using DeepLearning.Application.Common;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.FollowUpThreads.Commands.AddFollowUpMessage
{
    /// <summary>
    /// One more round in an already-open thread. Unlike Create/Close, this handler never touches
    /// the submission's state machine — the thread already holds it in under_dispute, and a
    /// per-round reply is purely conversational (FollowUpMessage.Verdict is informational only;
    /// see FollowUpThread's doc comment). MaxRounds caps how many times this can be called before
    /// the thread must be closed (CloseFollowUpThreadCommand) and, if still disputed, reopened —
    /// which the single-thread-per-submission design deliberately does NOT allow; the cap exists
    /// so an unbounded conversation can't blow up the prompt fed to the AI each round.
    /// </summary>
    public class AddFollowUpMessageCommandHandler : IRequestHandler<AddFollowUpMessageCommand, FollowUpThreadResult>
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

        public AddFollowUpMessageCommandHandler(
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

        public async Task<FollowUpThreadResult> Handle(AddFollowUpMessageCommand request, CancellationToken cancellationToken)
        {
            _ = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
                ?? throw new NotFoundException(nameof(User), request.UserId);

            var thread = await _followUpThreadRepository.GetByIdWithMessagesAsync(request.ThreadId, cancellationToken)
                ?? throw new NotFoundException(nameof(FollowUpThread), request.ThreadId);

            if (thread.Status != FollowUpThreadStatus.open)
            {
                throw new ConflictException($"Follow-up thread '{thread.Id}' is not open.");
            }

            if (thread.Messages.Count(m => m.Role == FollowUpMessageRole.user) >= FollowUpThread.MaxRounds)
            {
                throw new ConflictException(
                    $"Follow-up thread '{thread.Id}' has reached the maximum of {FollowUpThread.MaxRounds} rounds — close it to continue.");
            }

            var submission = await _submissionRepository.GetByIdAsync(thread.SubmissionId, cancellationToken)
                ?? throw new NotFoundException(nameof(Submission), thread.SubmissionId);

            var question = await _questionRepository.GetByIdAsync(submission.QuestionId, cancellationToken)
                ?? throw new NotFoundException(nameof(Question), submission.QuestionId);

            var aiCallLog = new AiCallLog
            {
                Id = Guid.NewGuid(),
                RequestType = AiOperationType.followup,
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

            var priorMessages = thread.Messages.ToList();
            var askedAt = DateTimeOffset.UtcNow;

            FollowUpTurnPayload payload;
            try
            {
                var model = FollowUpThreadSupport.BuildTemplateModel(
                    request.QuestionText, thread.ContextRef, submission, question, context, history: priorMessages);
                var prompt = await _examConfigLoader.BuildPromptAsync(thread.ExamTypeId, AiOperationType.followup, model, cancellationToken);

                var llmClient = await _llmClientResolver.GetActiveClientAsync(cancellationToken);
                payload = await AdaptiveCompletionRunner.RunAsync(
                    _aiCallRetryExecutor,
                    llmClient,
                    aiCallLog,
                    prompt,
                    initialBudget: AiOutputBudget.MediumInitial,
                    maxBudget: AiOutputBudget.MediumMax,
                    parse: FollowUpThreadSupport.ParsePayload<FollowUpTurnPayload>,
                    validate: FollowUpThreadSupport.NormaliseTurnPayload,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                // The thread stays open and under_dispute untouched — nothing to undo, this
                // round just failed to append; the user can retry.
                aiCallLog.Status = CallStatus.final_failure;
                aiCallLog.LastErrorMessage = $"Failed after {aiCallLog.AttemptCount} attempt(s): {ex.Message}";
                aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                throw new AiCallFailedException($"Follow-up reply could not be used: {ex.Message}", ex);
            }

            try
            {
                var userMessage = new FollowUpMessage
                {
                    Id = Guid.NewGuid(),
                    ThreadId = thread.Id,
                    Role = FollowUpMessageRole.user,
                    Content = request.QuestionText,
                    CreatedAt = askedAt,
                };
                var aiMessage = new FollowUpMessage
                {
                    Id = Guid.NewGuid(),
                    ThreadId = thread.Id,
                    Role = FollowUpMessageRole.ai,
                    Content = payload.AiResponse,
                    Verdict = payload.Verdict,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                thread.Messages.Add(userMessage);
                thread.Messages.Add(aiMessage);

                await _followUpThreadRepository.AddMessageAsync(userMessage, cancellationToken);
                await _followUpThreadRepository.AddMessageAsync(aiMessage, cancellationToken);

                aiCallLog.Status = CallStatus.success;
                aiCallLog.RelatedId = aiMessage.Id;
                aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return FollowUpThreadResult.From(thread, submission.Status, standardOverrideStatus: null);
            }
            catch (Exception ex)
            {
                aiCallLog.Status = CallStatus.final_failure;
                aiCallLog.LastErrorMessage = $"Failed to persist follow-up reply: {ex.Message}";
                aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                throw new AiCallFailedException($"Follow-up reply could not be used: {ex.Message}", ex);
            }
        }
    }
}
