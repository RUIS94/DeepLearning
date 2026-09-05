using DeepLearning.Application.Common;
using DeepLearning.Application.Features.StandardOverrides;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Events;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.FollowUpThreads.Commands.CloseFollowUpThread
{
    /// <summary>
    /// The "结算" step design decision (2026-09-02) moved out of every round and into here: a
    /// separate AiOperationType.followup_summary call sees the thread's entire message history
    /// and hands back ONE final verdict, exactly like CreateFollowUpQuestionCommandHandler used
    /// to do per single-shot question. Persist logic below is a straight port of that retired
    /// handler's post-AI-call half (StandardOverride creation, activation-threshold check,
    /// submission TransitionTo) — see StandardOverride.TriggeredByFollowUpThreadId's doc comment
    /// for why it writes a different FK column than the old flow did.
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
            _aiCallLogRepository = aiCallLogRepository;
            _examConfigLoader = examConfigLoader;
            _llmClientResolver = llmClientResolver;
            _aiCallRetryExecutor = aiCallRetryExecutor;
            _unitOfWork = unitOfWork;
        }

        public async Task<FollowUpThreadResult> Handle(CloseFollowUpThreadCommand request, CancellationToken cancellationToken)
        {
            _ = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
                ?? throw new NotFoundException(nameof(User), request.UserId);

            var thread = await _followUpThreadRepository.GetByIdWithMessagesAsync(request.ThreadId, cancellationToken)
                ?? throw new NotFoundException(nameof(FollowUpThread), request.ThreadId);

            if (thread.Status != FollowUpThreadStatus.open)
            {
                throw new ConflictException($"Follow-up thread '{thread.Id}' is already closed.");
            }

            var submission = await _submissionRepository.GetByIdAsync(thread.SubmissionId, cancellationToken)
                ?? throw new NotFoundException(nameof(Submission), thread.SubmissionId);

            var question = await _questionRepository.GetByIdAsync(submission.QuestionId, cancellationToken)
                ?? throw new NotFoundException(nameof(Question), submission.QuestionId);

            var aiCallLog = new AiCallLog
            {
                Id = Guid.NewGuid(),
                RequestType = AiOperationType.followup_summary,
                RelatedId = thread.Id,
                Status = CallStatus.calling,
                AttemptCount = 1,
                MaxRetries = 3,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await _aiCallLogRepository.AddAsync(aiCallLog, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var context = await FollowUpThreadSupport.LoadContextAsync(
                thread.ExamTypeId, submission, question,
                _assessmentDimensionRepository, _errorTaxonomyRepository, _submissionRepository, _referenceTranslationRepository,
                cancellationToken);
            var dimensionKeys = FollowUpThreadSupport.DimensionKeys(context);

            FollowUpSummaryPayload payload;
            try
            {
                var model = FollowUpThreadSupport.BuildTemplateModel(
                    questionText: string.Empty, thread.ContextRef, submission, question, context, history: thread.Messages);
                var prompt = await _examConfigLoader.BuildPromptAsync(thread.ExamTypeId, AiOperationType.followup_summary, model, cancellationToken);

                var llmClient = await _llmClientResolver.GetActiveClientAsync(cancellationToken);
                payload = await AdaptiveCompletionRunner.RunAsync(
                    _aiCallRetryExecutor,
                    llmClient,
                    aiCallLog,
                    prompt,
                    initialBudget: AiOutputBudget.MediumInitial,
                    maxBudget: AiOutputBudget.MediumMax,
                    parse: FollowUpThreadSupport.ParsePayload<FollowUpSummaryPayload>,
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

                aiCallLog.Status = CallStatus.success;
                aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;

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

                aiCallLog.Status = CallStatus.final_failure;
                aiCallLog.LastErrorMessage = $"Failed to persist follow-up thread close: {ex.Message}";
                aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
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
    }
}
