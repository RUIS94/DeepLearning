using DeepLearning.Application.Common;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.FollowUpThreads.Queries.PreviewFollowUpClose
{
    /// <summary>
    /// Draft-only: same AI call CloseFollowUpThreadCommandHandler would make (followup_summary
    /// for a dispute thread, score_challenge_summary for a score_challenge thread), but nothing
    /// is committed. Writes an AiCallLog so the call is still traced/costed.
    /// </summary>
    public class PreviewFollowUpCloseQueryHandler : IRequestHandler<PreviewFollowUpCloseQuery, FollowUpClosePreview>
    {
        private readonly IFollowUpThreadRepository _followUpThreadRepository;
        private readonly IUserRepository _userRepository;
        private readonly ISubmissionRepository _submissionRepository;
        private readonly IQuestionRepository _questionRepository;
        private readonly IAssessmentDimensionRepository _assessmentDimensionRepository;
        private readonly IErrorTaxonomyRepository _errorTaxonomyRepository;
        private readonly IReferenceTranslationRepository _referenceTranslationRepository;
        private readonly IAiCallLogRepository _aiCallLogRepository;
        private readonly IExamConfigLoader _examConfigLoader;
        private readonly ILlmClientResolver _llmClientResolver;
        private readonly IAiCallRetryExecutor _aiCallRetryExecutor;
        private readonly IUnitOfWork _unitOfWork;

        public PreviewFollowUpCloseQueryHandler(
            IFollowUpThreadRepository followUpThreadRepository,
            IUserRepository userRepository,
            ISubmissionRepository submissionRepository,
            IQuestionRepository questionRepository,
            IAssessmentDimensionRepository assessmentDimensionRepository,
            IErrorTaxonomyRepository errorTaxonomyRepository,
            IReferenceTranslationRepository referenceTranslationRepository,
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
            _aiCallLogRepository = aiCallLogRepository;
            _examConfigLoader = examConfigLoader;
            _llmClientResolver = llmClientResolver;
            _aiCallRetryExecutor = aiCallRetryExecutor;
            _unitOfWork = unitOfWork;
        }

        public async Task<FollowUpClosePreview> Handle(PreviewFollowUpCloseQuery request, CancellationToken cancellationToken)
        {
            _ = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
                ?? throw new NotFoundException(nameof(User), request.UserId);

            var thread = await _followUpThreadRepository.GetByIdWithMessagesAsync(request.ThreadId, cancellationToken)
                ?? throw new NotFoundException(nameof(FollowUpThread), request.ThreadId);

            if (thread.Status != FollowUpThreadStatus.open)
            {
                throw new ConflictException($"Follow-up thread '{thread.Id}' is already closed.");
            }

            if (thread.Kind == FollowUpThreadKind.knowledge)
            {
                throw new ConflictException(
                    $"Follow-up thread '{thread.Id}' is a knowledge thread — it has no summary to draft, close it directly.");
            }

            var submission = await _submissionRepository.GetByIdAsync(thread.SubmissionId, cancellationToken)
                ?? throw new NotFoundException(nameof(Submission), thread.SubmissionId);
            var question = await _questionRepository.GetByIdAsync(submission.QuestionId, cancellationToken)
                ?? throw new NotFoundException(nameof(Question), submission.QuestionId);

            var isScoreChallenge = thread.Kind == FollowUpThreadKind.score_challenge;
            var operationType = isScoreChallenge
                ? AiOperationType.score_challenge_summary
                : AiOperationType.followup_summary;

            var aiCallLog = new AiCallLog
            {
                Id = Guid.NewGuid(),
                RequestType = operationType,
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

            var model = FollowUpThreadSupport.BuildTemplateModel(
                thread.Kind.ToString(), questionText: string.Empty, thread.ContextRef, submission, question, context,
                history: thread.Messages, challengedDimensionId: thread.DimensionId);
            var prompt = await _examConfigLoader.BuildPromptAsync(thread.ExamTypeId, operationType, model, cancellationToken);
            var llmClient = await _llmClientResolver.GetActiveClientAsync(operationType, cancellationToken);

            try
            {
                if (isScoreChallenge)
                {
                    var payload = await AdaptiveCompletionRunner.RunAsync(
                        _aiCallRetryExecutor, llmClient, aiCallLog, prompt,
                        initialBudget: AiOutputBudget.MediumInitial, maxBudget: AiOutputBudget.MediumMax,
                        parse: LlmJson.Parse<ScoreChallengeSummaryPayload>,
                        validate: FollowUpThreadSupport.ValidateScoreChallengePayload,
                        cancellationToken: cancellationToken);

                    aiCallLog.Status = CallStatus.success;
                    aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    var current = context.GradingResults.FirstOrDefault(r => r.DimensionId == thread.DimensionId);
                    var dimKey = context.Dimensions.FirstOrDefault(d => d.Id == thread.DimensionId)?.DimensionKey;
                    return new FollowUpClosePreview(
                        thread.Kind, payload.AiResponse, FinalVerdict: null, StandardRevision: null,
                        payload.Decision, payload.RevisedBand, payload.RevisedRationale,
                        CurrentBand: current?.Band, ChallengedDimensionKey: dimKey);
                }
                else
                {
                    var dimensionKeys = FollowUpThreadSupport.DimensionKeys(context);
                    var payload = await AdaptiveCompletionRunner.RunAsync(
                        _aiCallRetryExecutor, llmClient, aiCallLog, prompt,
                        initialBudget: AiOutputBudget.MediumInitial, maxBudget: AiOutputBudget.MediumMax,
                        parse: LlmJson.Parse<FollowUpSummaryPayload>,
                        validate: p => FollowUpThreadSupport.ValidateSummaryPayload(p, dimensionKeys),
                        cancellationToken: cancellationToken);

                    aiCallLog.Status = CallStatus.success;
                    aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
                    await _unitOfWork.SaveChangesAsync(cancellationToken);

                    var revision = payload.StandardRevision is null
                        ? null
                        : new StandardRevisionPreview(
                            payload.StandardRevision.Scope, payload.StandardRevision.DimensionOrRule,
                            payload.StandardRevision.OriginalRuleText, payload.StandardRevision.RevisedRuleText);
                    return new FollowUpClosePreview(
                        thread.Kind, payload.AiResponse, payload.FinalVerdict, revision,
                        Decision: null, RevisedBand: null, RevisedRationale: null,
                        CurrentBand: null, ChallengedDimensionKey: null);
                }
            }
            catch (Exception ex)
            {
                aiCallLog.Status = CallStatus.final_failure;
                aiCallLog.LastErrorMessage = $"Failed after {aiCallLog.AttemptCount} attempt(s): {ex.Message}";
                aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                throw new AiCallFailedException($"Could not draft the summary: {ex.Message}", ex);
            }
        }
    }
}
