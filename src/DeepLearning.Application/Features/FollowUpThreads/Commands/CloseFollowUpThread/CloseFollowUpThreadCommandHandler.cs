using DeepLearning.Application.Common;
using DeepLearning.Application.Features.StandardOverrides;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.FollowUpThreads.Commands.CloseFollowUpThread
{
    /// <summary>
    /// The "结算" step. Three shapes:
    ///   - knowledge thread → nothing to adjudicate: close it, release the submission, no AI call.
    ///   - dispute / score_challenge with request.Input → commit the user-reviewed summary as-is
    ///     (no AI call — the frontend already drafted it via PreviewFollowUpCloseQuery).
    ///   - dispute / score_challenge without Input → run the summary AI call and commit its output
    ///     (the original one-shot path; kept for tests and a "just close it" caller).
    /// Commit side effects are unchanged: StandardOverride creation + activation-threshold check
    /// for a dispute; GradingResult.Band rewrite + GradingResultRevision + regraded for a
    /// score_challenge adjust.
    /// </summary>
    public class CloseFollowUpThreadCommandHandler : IRequestHandler<CloseFollowUpThreadCommand, FollowUpThreadResult>
    {
        private readonly IFollowUpThreadRepository _followUpThreadRepository;
        private readonly IUserRepository _userRepository;
        private readonly ISubmissionRepository _submissionRepository;
        private readonly IQuestionRepository _questionRepository;
        private readonly IAssessmentDimensionRepository _assessmentDimensionRepository;
        private readonly IErrorTaxonomyRepository _errorTaxonomyRepository;
        private readonly IReferenceTranslationRepository _referenceTranslationRepository;
        private readonly IStandardOverrideRepository _standardOverrideRepository;
        private readonly IGenerationPolicyRepository _generationPolicyRepository;
        private readonly IGradingResultRevisionRepository _gradingResultRevisionRepository;
        private readonly IGradingSummaryRepository _gradingSummaryRepository;
        private readonly IEnumerable<IGradingResultInterpreter> _interpreters;
        private readonly IAiCallLogRepository _aiCallLogRepository;
        private readonly IExamConfigLoader _examConfigLoader;
        private readonly ILlmClientResolver _llmClientResolver;
        private readonly IAiCallRetryExecutor _aiCallRetryExecutor;
        private readonly IUnitOfWork _unitOfWork;

        public CloseFollowUpThreadCommandHandler(
            IFollowUpThreadRepository followUpThreadRepository,
            IUserRepository userRepository,
            ISubmissionRepository submissionRepository,
            IQuestionRepository questionRepository,
            IAssessmentDimensionRepository assessmentDimensionRepository,
            IErrorTaxonomyRepository errorTaxonomyRepository,
            IReferenceTranslationRepository referenceTranslationRepository,
            IStandardOverrideRepository standardOverrideRepository,
            IGenerationPolicyRepository generationPolicyRepository,
            IGradingResultRevisionRepository gradingResultRevisionRepository,
            IGradingSummaryRepository gradingSummaryRepository,
            IEnumerable<IGradingResultInterpreter> interpreters,
            IAiCallLogRepository aiCallLogRepository,
            IExamConfigLoader examConfigLoader,
            ILlmClientResolver llmClientResolver,
            IAiCallRetryExecutor aiCallRetryExecutor,
            IUnitOfWork unitOfWork)
        {
            _followUpThreadRepository = followUpThreadRepository;
            _userRepository = userRepository;
            _submissionRepository = submissionRepository;
            _questionRepository = questionRepository;
            _assessmentDimensionRepository = assessmentDimensionRepository;
            _errorTaxonomyRepository = errorTaxonomyRepository;
            _referenceTranslationRepository = referenceTranslationRepository;
            _standardOverrideRepository = standardOverrideRepository;
            _generationPolicyRepository = generationPolicyRepository;
            _gradingResultRevisionRepository = gradingResultRevisionRepository;
            _gradingSummaryRepository = gradingSummaryRepository;
            _interpreters = interpreters;
            _aiCallLogRepository = aiCallLogRepository;
            _examConfigLoader = examConfigLoader;
            _llmClientResolver = llmClientResolver;
            _aiCallRetryExecutor = aiCallRetryExecutor;
            _unitOfWork = unitOfWork;
        }

        public async Task<FollowUpThreadResult> Handle(CloseFollowUpThreadCommand request, CancellationToken cancellationToken)
        {
            await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
                .EnsureFoundAsync(nameof(User), request.UserId);

            var thread = await _followUpThreadRepository.GetByIdWithMessagesAsync(request.ThreadId, cancellationToken)
                ?? throw new NotFoundException(nameof(FollowUpThread), request.ThreadId);

            if (thread.Status != FollowUpThreadStatus.open)
            {
                throw new ConflictException($"Follow-up thread '{thread.Id}' is already closed.");
            }

            var submission = await _submissionRepository.GetByIdAsync(thread.SubmissionId, cancellationToken)
                ?? throw new NotFoundException(nameof(Submission), thread.SubmissionId);

            if (submission.UserId != request.UserId)
            {
                throw new NotFoundException(nameof(FollowUpThread), request.ThreadId);
            }

            var question = await _questionRepository.GetByIdAsync(submission.QuestionId, cancellationToken)
                ?? throw new NotFoundException(nameof(Question), submission.QuestionId);

            // A pure-knowledge thread never disputed anything — no summary, no verdict, no AI call.
            // SkipSummary opts a dispute / score_challenge into the same "just close it" behaviour:
            // no AI call, no verdict, no StandardOverride, no Band rewrite.
            if (thread.Kind == FollowUpThreadKind.knowledge || request.SkipSummary)
            {
                thread.Status = FollowUpThreadStatus.closed;
                thread.FinalVerdict = null;
                thread.ClosedAt = DateTimeOffset.UtcNow;
                submission.TransitionTo(SubmissionStatus.graded);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return FollowUpThreadResult.From(thread, submission.Status, standardOverrideStatus: null);
            }

            var context = await FollowUpThreadSupport.LoadContextAsync(
                thread.ExamTypeId, submission, question,
                _assessmentDimensionRepository, _errorTaxonomyRepository, _submissionRepository, _referenceTranslationRepository,
                cancellationToken);

            if (thread.Kind == FollowUpThreadKind.score_challenge)
            {
                return await CloseScoreChallengeAsync(thread, submission, question, context, request.Input, cancellationToken);
            }

            var dimensionKeys = FollowUpThreadSupport.DimensionKeys(context);

            FollowUpSummaryPayload payload;
            AiCallLog? aiCallLog = null;
            if (request.Input is { } input)
            {
                payload = new FollowUpSummaryPayload
                {
                    AiResponse = input.AiResponse,
                    FinalVerdict = input.FinalVerdict,
                    StandardRevision = input.StandardRevision is null ? null : new StandardRevisionPayload
                    {
                        Scope = input.StandardRevision.Scope,
                        DimensionOrRule = input.StandardRevision.DimensionOrRule,
                        OriginalRuleText = input.StandardRevision.OriginalRuleText,
                        RevisedRuleText = input.StandardRevision.RevisedRuleText,
                    },
                };
                FollowUpThreadSupport.ValidateSummaryPayload(payload, dimensionKeys);
            }
            else
            {
                aiCallLog = AiCallLogFactory.New(AiOperationType.followup_summary, thread.Id);
                await _aiCallLogRepository.AddAsync(aiCallLog, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                try
                {
                    var model = FollowUpThreadSupport.BuildTemplateModel(
                        thread.Kind.ToString(), questionText: string.Empty, thread.ContextRef, submission, question, context,
                        history: thread.Messages, challengedDimensionId: thread.DimensionId);
                    var prompt = await _examConfigLoader.BuildPromptAsync(thread.ExamTypeId, AiOperationType.followup_summary, model, cancellationToken);

                    var llmClient = await _llmClientResolver.GetActiveClientAsync(AiOperationType.followup_summary, cancellationToken);
                    payload = await AdaptiveCompletionRunner.RunAsync(
                        _aiCallRetryExecutor,
                        llmClient,
                        aiCallLog,
                        prompt,
                        initialBudget: AiOutputBudget.MediumInitial,
                        maxBudget: AiOutputBudget.MediumMax,
                        parse: LlmJson.Parse<FollowUpSummaryPayload>,
                        validate: p => FollowUpThreadSupport.ValidateSummaryPayload(p, dimensionKeys),
                        cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    // The thread stays open (not closed) and the submission stays under_dispute
                    // untouched — the dispute is still unresolved, the user can try closing again.
                    aiCallLog.Status = CallStatus.final_failure;
                    aiCallLog.LastErrorMessage = $"Failed after {aiCallLog.AttemptCount} attempt(s): {ex.Message}";
                    aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    throw new AiCallFailedException($"Follow-up thread could not be closed: {ex.Message}", ex);
                }
            }

            try
            {
                StandardOverride? newOverride = null;
                if (payload.FinalVerdict == FollowUpVerdict.user_correct)
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
                        TriggeredByFollowUpThreadId = thread.Id,
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

                thread.Status = FollowUpThreadStatus.closed;
                thread.FinalVerdict = payload.FinalVerdict;
                thread.StandardOverrideId = newOverride?.Id;
                thread.ClosedAt = DateTimeOffset.UtcNow;

                if (aiCallLog is not null)
                {
                    aiCallLog.Status = CallStatus.success;
                    aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
                }

                // Persisted before the activation-threshold count below — that count queries the
                // database directly, so this row must actually be committed first for the count
                // to include it (same reasoning as the retired CreateFollowUpQuestionCommandHandler).
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                if (newOverride is not null)
                {
                    await TryAutoActivateAsync(thread.ExamTypeId, newOverride, cancellationToken);
                }

                return FollowUpThreadResult.From(thread, submission.Status, newOverride?.Status);
            }
            catch (Exception ex)
            {
                // If the failure happened on the final SaveChangesAsync itself, TransitionTo(Graded)
                // above may already have run in-memory with nothing actually committed — reset so
                // the guard below's own TransitionTo(Graded) is still legal (submission started
                // this handler at under_dispute, the only other state graded can follow).
                if (submission.Status == SubmissionStatus.graded)
                {
                    submission.Status = SubmissionStatus.under_dispute;
                }

                if (aiCallLog is not null)
                {
                    aiCallLog.Status = CallStatus.final_failure;
                    aiCallLog.LastErrorMessage = $"Failed to persist follow-up thread close: {ex.Message}";
                    aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
                }
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                throw new AiCallFailedException($"Follow-up thread could not be closed: {ex.Message}", ex);
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

            StandardOverride? previous = null;
            if (candidate.PreviousOverrideId is { } previousId)
            {
                previous = await _standardOverrideRepository.GetByIdAsync(previousId, cancellationToken);
            }

            StandardOverrideActivation.Activate(candidate, previous, DateTimeOffset.UtcNow);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Closing path for a score_challenge thread. `input` non-null = commit the user-reviewed
        /// decision (no AI call); null = run the score_challenge_summary call first. adjust
        /// rewrites the disputed dimension's GradingResult.Band in place, writes a
        /// GradingResultRevision audit row, recomputes PassBool (via the same
        /// IGradingResultInterpreter grading uses) and the GradingSummary roll-up, and moves the
        /// submission to `regraded`. uphold just returns it to `graded`. Never touches
        /// StandardOverride — a score challenge fixes this score, it does not patch the rubric.
        /// </summary>
        private async Task<FollowUpThreadResult> CloseScoreChallengeAsync(
            FollowUpThread thread,
            Submission submission,
            Question question,
            FollowUpThreadContext context,
            FollowUpCloseInput? input,
            CancellationToken cancellationToken)
        {
            ScoreChallengeSummaryPayload payload;
            AiCallLog? aiCallLog = null;
            if (input is not null)
            {
                payload = new ScoreChallengeSummaryPayload
                {
                    AiResponse = input.AiResponse,
                    Decision = input.Decision ?? ScoreChallengeDecision.uphold,
                    RevisedBand = input.RevisedBand,
                    RevisedRationale = input.RevisedRationale,
                };
                FollowUpThreadSupport.ValidateScoreChallengePayload(payload);
            }
            else
            {
                aiCallLog = AiCallLogFactory.New(AiOperationType.score_challenge_summary, thread.Id);
                await _aiCallLogRepository.AddAsync(aiCallLog, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                try
                {
                    var model = FollowUpThreadSupport.BuildTemplateModel(
                        thread.Kind.ToString(), questionText: string.Empty, thread.ContextRef, submission, question, context,
                        history: thread.Messages, challengedDimensionId: thread.DimensionId);
                    var prompt = await _examConfigLoader.BuildPromptAsync(thread.ExamTypeId, AiOperationType.score_challenge_summary, model, cancellationToken);

                    var llmClient = await _llmClientResolver.GetActiveClientAsync(AiOperationType.score_challenge_summary, cancellationToken);
                    payload = await AdaptiveCompletionRunner.RunAsync(
                        _aiCallRetryExecutor,
                        llmClient,
                        aiCallLog,
                        prompt,
                        initialBudget: AiOutputBudget.MediumInitial,
                        maxBudget: AiOutputBudget.MediumMax,
                        parse: LlmJson.Parse<ScoreChallengeSummaryPayload>,
                        validate: FollowUpThreadSupport.ValidateScoreChallengePayload,
                        cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    // Thread stays open, submission stays under_dispute — the challenge is unresolved.
                    aiCallLog.Status = CallStatus.final_failure;
                    aiCallLog.LastErrorMessage = $"Failed after {aiCallLog.AttemptCount} attempt(s): {ex.Message}";
                    aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    throw new AiCallFailedException($"Score challenge could not be closed: {ex.Message}", ex);
                }
            }

            try
            {
                if (payload.Decision == ScoreChallengeDecision.adjust)
                {
                    var gradingResult = context.GradingResults.Single(r => r.DimensionId == thread.DimensionId!.Value);
                    var dimension = context.Dimensions.Single(d => d.Id == thread.DimensionId!.Value);
                    var fromBand = gradingResult.Band;
                    var toBand = payload.RevisedBand!.Value;

                    var interpretation = _interpreters.First(i => i.ScaleType == dimension.ScaleType)
                        .Interpret(toBand.ToString(), dimension.PassThreshold);

                    await _gradingResultRevisionRepository.AddAsync(new GradingResultRevision
                    {
                        Id = Guid.NewGuid(),
                        SubmissionId = submission.Id,
                        DimensionId = dimension.Id,
                        GradingResultId = gradingResult.Id,
                        FromBand = fromBand,
                        ToBand = toBand,
                        Reason = payload.RevisedRationale!,
                        TriggeredByFollowUpThreadId = thread.Id,
                        CreatedAt = DateTimeOffset.UtcNow,
                    }, cancellationToken);

                    gradingResult.Band = interpretation.Band;
                    gradingResult.PassBool = interpretation.PassBool;
                    gradingResult.Rationale =
                        $"{gradingResult.Rationale}\n\n[追问改判 Band {fromBand}→{toBand}] {payload.RevisedRationale}";
                    // The AI-derived confidence / probabilities described the original Band, not this one.
                    gradingResult.Confidence = null;
                    gradingResult.EstimatedPassProbability = null;
                    gradingResult.AlternativeBand = null;

                    await RecomputeGradingSummaryAsync(submission.Id, context.GradingResults, cancellationToken);

                    submission.TransitionTo(SubmissionStatus.regraded);
                }
                else
                {
                    submission.TransitionTo(SubmissionStatus.graded);
                }

                thread.Status = FollowUpThreadStatus.closed;
                thread.ClosedAt = DateTimeOffset.UtcNow;

                if (aiCallLog is not null)
                {
                    aiCallLog.Status = CallStatus.success;
                    aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return FollowUpThreadResult.From(thread, submission.Status, standardOverrideStatus: null);
            }
            catch (Exception ex)
            {
                // Mirror the followup-summary path: an in-memory TransitionTo may have run with
                // nothing committed — reset so the failure guard's own bookkeeping is legal.
                if (submission.Status is SubmissionStatus.graded or SubmissionStatus.regraded)
                {
                    submission.Status = SubmissionStatus.under_dispute;
                }

                if (aiCallLog is not null)
                {
                    aiCallLog.Status = CallStatus.final_failure;
                    aiCallLog.LastErrorMessage = $"Failed to persist score challenge close: {ex.Message}";
                    aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
                }
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                throw new AiCallFailedException($"Score challenge could not be closed: {ex.Message}", ex);
            }
        }

        private async Task RecomputeGradingSummaryAsync(
            Guid submissionId, IReadOnlyList<GradingResult> gradingResults, CancellationToken cancellationToken)
        {
            var summary = await _gradingSummaryRepository.GetBySubmissionIdAsync(submissionId, cancellationToken);
            if (summary is null)
            {
                return;
            }

            summary.OverallPassBool = gradingResults.All(r => r.PassBool);
            // Best effort: the re-graded dimension now contributes 1.0 (its estimate was cleared).
            summary.OverallPassProbability = gradingResults.Aggregate(1m, (acc, r) => acc * (r.EstimatedPassProbability ?? 1m));
        }
    }
}
