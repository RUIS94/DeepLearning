using System.Text.Json;
using System.Text.Json.Serialization;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Events;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.FollowUps.Commands.CreateFollowUpQuestion
{
    /// <summary>
    /// Orchestrates the 追问 flow (design doc §2.1 nodes Q/R/S/T, §10.6): the submission must
    /// already be Graded — TransitionTo(under_dispute) enforces this, so a submission still
    /// Grading/GradingFailed/etc. gets a 409 instead of being silently accepted. The AI answers
    /// the user's question and returns a verdict; user_correct additionally may record a
    /// standard_overrides correction note — NOT a rewrite of the official rubric text, which
    /// stays authoritative and untouched, but a patch to how the AI applies it (the AI misread
    /// the source text, or missed an error actually present in the user's translation — see
    /// add_followup_prompt_template.sql for the exact framing given to the model). It starts
    /// life as 'observing' (never immediately authoritative) and is only promoted to 'active'
    /// once the same correction has been independently confirmed on enough distinct questions
    /// (StandardOverrideActivationPolicy) — or via the separate ActivateStandardOverride command
    /// for the "经过一次人工复核" path §10.6 also allows. The submission always ends back at
    /// Graded either way: user_incorrect/partial go straight back, user_correct passes through
    /// StandardRevised first per §4.1's state machine (Graded -> UnderDispute -> StandardRevised
    /// -> Graded).
    /// </summary>
    public class CreateFollowUpQuestionCommandHandler : IRequestHandler<CreateFollowUpQuestionCommand, CreateFollowUpQuestionResult>
    {
        private static readonly JsonSerializerOptions PayloadJsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly IExamTypeRepository _examTypeRepository;
        private readonly IUserRepository _userRepository;
        private readonly ISubmissionRepository _submissionRepository;
        private readonly IQuestionRepository _questionRepository;
        private readonly IAssessmentDimensionRepository _assessmentDimensionRepository;
        private readonly IErrorTaxonomyRepository _errorTaxonomyRepository;
        private readonly IFollowUpQuestionRepository _followUpQuestionRepository;
        private readonly IStandardOverrideRepository _standardOverrideRepository;
        private readonly IGenerationPolicyRepository _generationPolicyRepository;
        private readonly IAiCallLogRepository _aiCallLogRepository;
        private readonly IExamConfigLoader _examConfigLoader;
        private readonly ILlmClientResolver _llmClientResolver;
        private readonly IUnitOfWork _unitOfWork;

        public CreateFollowUpQuestionCommandHandler(
            IExamTypeRepository examTypeRepository,
            IUserRepository userRepository,
            ISubmissionRepository submissionRepository,
            IQuestionRepository questionRepository,
            IAssessmentDimensionRepository assessmentDimensionRepository,
            IErrorTaxonomyRepository errorTaxonomyRepository,
            IFollowUpQuestionRepository followUpQuestionRepository,
            IStandardOverrideRepository standardOverrideRepository,
            IGenerationPolicyRepository generationPolicyRepository,
            IAiCallLogRepository aiCallLogRepository,
            IExamConfigLoader examConfigLoader,
            ILlmClientResolver llmClientResolver,
            IUnitOfWork unitOfWork)
        {
            _examTypeRepository = examTypeRepository;
            _userRepository = userRepository;
            _submissionRepository = submissionRepository;
            _questionRepository = questionRepository;
            _assessmentDimensionRepository = assessmentDimensionRepository;
            _errorTaxonomyRepository = errorTaxonomyRepository;
            _followUpQuestionRepository = followUpQuestionRepository;
            _standardOverrideRepository = standardOverrideRepository;
            _generationPolicyRepository = generationPolicyRepository;
            _aiCallLogRepository = aiCallLogRepository;
            _examConfigLoader = examConfigLoader;
            _llmClientResolver = llmClientResolver;
            _unitOfWork = unitOfWork;
        }

        public async Task<CreateFollowUpQuestionResult> Handle(CreateFollowUpQuestionCommand request, CancellationToken cancellationToken)
        {
            _ = await _examTypeRepository.GetByIdAsync(request.ExamTypeId, cancellationToken)
                ?? throw new NotFoundException(nameof(ExamType), request.ExamTypeId);

            _ = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
                ?? throw new NotFoundException(nameof(User), request.UserId);

            var submission = await _submissionRepository.GetByIdAsync(request.SubmissionId, cancellationToken)
                ?? throw new NotFoundException(nameof(Submission), request.SubmissionId);

            var question = await _questionRepository.GetByIdAsync(submission.QuestionId, cancellationToken)
                ?? throw new NotFoundException(nameof(Question), submission.QuestionId);

            // Only legal from Graded — throws InvalidSubmissionStateException (409) if the
            // submission is still being graded, failed, archived, etc.
            submission.TransitionTo(SubmissionStatus.under_dispute);

            var aiCallLog = new AiCallLog
            {
                Id = Guid.NewGuid(),
                RequestType = AiOperationType.followup,
                RelatedId = submission.Id,
                Status = CallStatus.calling,
                AttemptCount = 1,
                MaxRetries = 3,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await _aiCallLogRepository.AddAsync(aiCallLog, cancellationToken);
            // Persisted up front (submission's under_dispute status included) so both survive
            // even if the LLM call below never returns.
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var dimensions = await _assessmentDimensionRepository.ListByExamTypeAsync(request.ExamTypeId, submission.TaskType, cancellationToken);
            var errorTaxonomies = await _errorTaxonomyRepository.ListByExamTypeAsync(request.ExamTypeId, cancellationToken);
            var gradingResults = await _submissionRepository.GetGradingResultsAsync(submission.Id, cancellationToken);
            var errorList = await _submissionRepository.GetErrorListAsync(submission.Id, cancellationToken);

            LlmCompletionResult completion;
            try
            {
                var templateModel = BuildTemplateModel(request, submission, question, dimensions, errorTaxonomies, gradingResults, errorList);
                var prompt = await _examConfigLoader.BuildPromptAsync(request.ExamTypeId, AiOperationType.followup, templateModel, cancellationToken);

                var llmClient = await _llmClientResolver.GetActiveClientAsync(cancellationToken);
                completion = await llmClient.CompleteAsync(
                    new LlmCompletionRequest(SystemPrompt: null, UserPrompt: prompt, MaxTokens: 4096),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                await FailAsync(submission, aiCallLog, ex.Message, cancellationToken);
                throw;
            }

            // Everything from here on — parsing, structured-output validation, entity
            // construction, and the final persist — is one try/catch, same reasoning as
            // GradeSubmissionCommandHandler: without it, a failure here would leave the
            // submission stuck in UnderDispute forever (there's no UnderDispute->UnderDispute
            // transition, so it could never be retried).
            try
            {
                var dimensionKeys = dimensions.Select(x => x.DimensionKey).ToHashSet();
                var payload = ParsePayload(completion.Text);
                ValidatePayload(payload, dimensionKeys);

                var followUp = new FollowUpQuestion
                {
                    Id = Guid.NewGuid(),
                    SubmissionId = submission.Id,
                    UserId = request.UserId,
                    ContextRef = request.ContextRef,
                    QuestionText = request.QuestionText,
                    AiResponse = payload.AiResponse,
                    Verdict = payload.Verdict,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                await _followUpQuestionRepository.AddAsync(followUp, cancellationToken);

                StandardOverride? newOverride = null;
                if (payload.Verdict == FollowUpVerdict.user_correct)
                {
                    var revision = payload.StandardRevision!;
                    var baseline = await _standardOverrideRepository.GetActiveByRuleAsync(revision.Scope, revision.DimensionOrRule, cancellationToken);

                    newOverride = new StandardOverride
                    {
                        Id = Guid.NewGuid(),
                        Scope = revision.Scope,
                        DimensionOrRule = revision.DimensionOrRule,
                        OriginalRuleText = revision.OriginalRuleText,
                        RevisedRuleText = revision.RevisedRuleText,
                        TriggeredByFollowupId = followUp.Id,
                        Status = OverrideStatus.observing,
                        PreviousOverrideId = baseline?.Id,
                        CreatedAt = DateTimeOffset.UtcNow,
                    };
                    await _standardOverrideRepository.AddAsync(newOverride, cancellationToken);

                    submission.TransitionTo(SubmissionStatus.standard_revised);
                    submission.TransitionTo(SubmissionStatus.graded);
                }
                else
                {
                    submission.TransitionTo(SubmissionStatus.graded);
                }

                aiCallLog.Status = CallStatus.success;
                aiCallLog.RelatedId = followUp.Id;
                aiCallLog.LatencyMs = completion.LatencyMs;
                aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;

                // Persisted before the activation-threshold count below — that count queries the
                // database directly (IStandardOverrideRepository.CountDistinctQuestionsPendingAsync),
                // so this row must actually be committed first for the count to include it.
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                if (newOverride is not null)
                {
                    await TryAutoActivateAsync(request.ExamTypeId, newOverride, cancellationToken);
                }

                return new CreateFollowUpQuestionResult(
                    followUp.Id,
                    submission.Id,
                    followUp.Verdict,
                    followUp.AiResponse ?? string.Empty,
                    submission.Status,
                    newOverride?.Id,
                    newOverride?.Status);
            }
            catch (Exception ex)
            {
                // If the failure happened on the final SaveChangesAsync itself, TransitionTo(Graded)
                // above may already have run in-memory with nothing actually committed — reset so
                // FailAsync's own TransitionTo(Graded) call is still legal, mirroring
                // GradeSubmissionCommandHandler's identical guard.
                if (submission.Status == SubmissionStatus.graded)
                {
                    submission.Status = SubmissionStatus.under_dispute;
                }

                await FailAsync(submission, aiCallLog, $"Failed to parse/validate/persist LLM response: {ex.Message}", cancellationToken);
                throw new AiCallFailedException($"Follow-up response could not be used: {ex.Message}", ex);
            }
        }

        private async Task TryAutoActivateAsync(Guid examTypeId, StandardOverride candidate, CancellationToken cancellationToken)
        {
            var policy = await _generationPolicyRepository.GetByKeyAsync(examTypeId, "override_activation_threshold", cancellationToken);
            var threshold = policy is not null
                ? StandardOverrideActivationPolicy.ParseThreshold(policy.PolicyValue)
                : StandardOverrideActivationPolicy.DefaultConfirmationsRequired;

            var confirmations = await _standardOverrideRepository.CountDistinctQuestionsPendingAsync(
                candidate.Scope, candidate.DimensionOrRule, candidate.PreviousOverrideId, cancellationToken);

            if (!StandardOverrideActivationPolicy.ShouldActivate(confirmations, threshold))
            {
                return;
            }

            candidate.Status = OverrideStatus.active;
            candidate.EffectiveFrom = DateTimeOffset.UtcNow;
            candidate.AddDomainEvent(new StandardOverrideActivatedEvent
            {
                StandardOverrideId = candidate.Id,
                Scope = candidate.Scope,
                DimensionOrRule = candidate.DimensionOrRule,
                PreviousOverrideId = candidate.PreviousOverrideId,
                ActivatedAt = candidate.EffectiveFrom.Value,
            });

            if (candidate.PreviousOverrideId is { } previousId)
            {
                var previous = await _standardOverrideRepository.GetByIdAsync(previousId, cancellationToken);
                if (previous is not null)
                {
                    previous.Status = OverrideStatus.deprecated;
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task FailAsync(Submission submission, AiCallLog aiCallLog, string errorMessage, CancellationToken cancellationToken)
        {
            submission.TransitionTo(SubmissionStatus.graded);
            aiCallLog.Status = CallStatus.final_failure;
            aiCallLog.LastErrorMessage = errorMessage;
            aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private static object BuildTemplateModel(
            CreateFollowUpQuestionCommand request,
            Submission submission,
            Question question,
            List<AssessmentDimension> dimensions,
            List<ErrorTaxonomy> errorTaxonomies,
            List<GradingResult> gradingResults,
            List<ErrorListItem> errorList) => new
            {
                QuestionText = request.QuestionText,
                ContextRef = request.ContextRef,
                TaskType = submission.TaskType.ToString(),
                SourceText = question.SourceText,
                SubmissionContent = submission.Content,
                GradingResults = gradingResults.Select(r => new
                {
                    DimensionKey = r.Dimension!.DimensionKey,
                    Band = r.Band,
                    Rationale = r.Rationale,
                }),
                Errors = errorList.Select(e => new
                {
                    PositionRef = e.PositionRef,
                    ErrorCategory = e.ErrorTaxonomy!.CategoryKey,
                    Explanation = e.Explanation,
                    ImpactsCore = e.ImpactsCore,
                }),
                Dimensions = dimensions.Select(d => new
                {
                    DimensionKey = d.DimensionKey,
                    DimensionName = d.DimensionName,
                }),
                ErrorTaxonomies = errorTaxonomies.Select(t => new
                {
                    CategoryKey = t.CategoryKey,
                    CategoryName = t.CategoryName,
                }),
            };

        private static FollowUpPayload ParsePayload(string rawText)
        {
            var json = StripMarkdownFence(rawText.Trim());
            return JsonSerializer.Deserialize<FollowUpPayload>(json, PayloadJsonOptions)
                ?? throw new InvalidOperationException("Deserialized to null.");
        }

        /// <summary>
        /// Same "structured output is a hard constraint, not a prompt reminder" philosophy as
        /// GradeSubmissionCommandHandler.ValidatePayload (design doc §10.3), applied to a
        /// proposed correction note: a grading_rubric-scoped dimensionOrRule must name a
        /// dimension_key we actually have on file for this exam type, not one the AI invented —
        /// it identifies which dimension's judgment the correction applies to, it does not rename
        /// or redefine that dimension. translation_reference scope has no equivalent taxonomy to
        /// check against, so it's only required to be non-empty.
        /// </summary>
        private static void ValidatePayload(FollowUpPayload payload, HashSet<string> dimensionKeys)
        {
            if (payload.Verdict == FollowUpVerdict.pending)
            {
                throw new InvalidOperationException("verdict must not be 'pending' — the AI must decide user_correct/user_incorrect/partial.");
            }

            if (payload.Verdict != FollowUpVerdict.user_correct)
            {
                return;
            }

            var revision = payload.StandardRevision
                ?? throw new InvalidOperationException("verdict=user_correct requires a standardRevision object.");

            if (string.IsNullOrWhiteSpace(revision.DimensionOrRule))
            {
                throw new InvalidOperationException("standardRevision.dimensionOrRule must not be empty.");
            }

            if (revision.Scope == OverrideScope.grading_rubric && !dimensionKeys.Contains(revision.DimensionOrRule))
            {
                throw new InvalidOperationException(
                    $"standardRevision.dimensionOrRule '{revision.DimensionOrRule}' is not a known assessment dimension for this exam type.");
            }

            if (string.IsNullOrWhiteSpace(revision.RevisedRuleText))
            {
                throw new InvalidOperationException("standardRevision.revisedRuleText must not be empty.");
            }
        }

        private static string StripMarkdownFence(string text)
        {
            if (!text.StartsWith("```", StringComparison.Ordinal))
            {
                return text;
            }

            var firstNewLine = text.IndexOf('\n');
            var withoutOpeningFence = firstNewLine >= 0 ? text[(firstNewLine + 1)..] : text;
            var closingFenceIndex = withoutOpeningFence.LastIndexOf("```", StringComparison.Ordinal);
            return closingFenceIndex >= 0 ? withoutOpeningFence[..closingFenceIndex] : withoutOpeningFence;
        }

        private class FollowUpPayload
        {
            public string AiResponse { get; set; } = string.Empty;

            [JsonConverter(typeof(JsonStringEnumConverter))]
            public FollowUpVerdict Verdict { get; set; }

            public StandardRevisionPayload? StandardRevision { get; set; }
        }

        private class StandardRevisionPayload
        {
            [JsonConverter(typeof(JsonStringEnumConverter))]
            public OverrideScope Scope { get; set; }

            public string DimensionOrRule { get; set; } = string.Empty;

            public string? OriginalRuleText { get; set; }

            public string RevisedRuleText { get; set; } = string.Empty;
        }
    }
}
