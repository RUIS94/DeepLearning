using DeepLearning.Application.Common;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.FollowUpThreads.Commands.CreateFollowUpThread
{
    /// <summary>
    /// Round 1 of a follow-up thread — see FollowUpThread's own doc comment for the overall
    /// design (single thread per submission, held under_dispute for the thread's whole open
    /// lifetime, per-round replies purely conversational). Structurally this is the "front half"
    /// of the retired CreateFollowUpQuestionCommandHandler: same AiCallLog/retry/validate
    /// scaffolding, but it stops after persisting the first message pair — no verdict-driven
    /// side effect happens here, that's CloseFollowUpThreadCommandHandler's job.
    /// </summary>
    public class CreateFollowUpThreadCommandHandler : IRequestHandler<CreateFollowUpThreadCommand, FollowUpThreadResult>
    {
        private readonly IExamTypeRepository _examTypeRepository;
        private readonly IUserRepository _userRepository;
        private readonly ISubmissionRepository _submissionRepository;
        private readonly IQuestionRepository _questionRepository;
        private readonly IAssessmentDimensionRepository _assessmentDimensionRepository;
        private readonly IErrorTaxonomyRepository _errorTaxonomyRepository;
        private readonly IFollowUpThreadRepository _followUpThreadRepository;
        private readonly IReferenceTranslationRepository _referenceTranslationRepository;
        private readonly IAiCallLogRepository _aiCallLogRepository;
        private readonly IExamConfigLoader _examConfigLoader;
        private readonly ILlmClientResolver _llmClientResolver;
        private readonly IAiCallRetryExecutor _aiCallRetryExecutor;
        private readonly IUnitOfWork _unitOfWork;

        public CreateFollowUpThreadCommandHandler(
            IExamTypeRepository examTypeRepository,
            IUserRepository userRepository,
            ISubmissionRepository submissionRepository,
            IQuestionRepository questionRepository,
            IAssessmentDimensionRepository assessmentDimensionRepository,
            IErrorTaxonomyRepository errorTaxonomyRepository,
            IFollowUpThreadRepository followUpThreadRepository,
            IReferenceTranslationRepository referenceTranslationRepository,
            IAiCallLogRepository aiCallLogRepository,
            IExamConfigLoader examConfigLoader,
            ILlmClientResolver llmClientResolver,
            IAiCallRetryExecutor aiCallRetryExecutor,
            IUnitOfWork unitOfWork)
        {
            _examTypeRepository = examTypeRepository;
            _userRepository = userRepository;
            _submissionRepository = submissionRepository;
            _questionRepository = questionRepository;
            _assessmentDimensionRepository = assessmentDimensionRepository;
            _errorTaxonomyRepository = errorTaxonomyRepository;
            _followUpThreadRepository = followUpThreadRepository;
            _referenceTranslationRepository = referenceTranslationRepository;
            _aiCallLogRepository = aiCallLogRepository;
            _examConfigLoader = examConfigLoader;
            _llmClientResolver = llmClientResolver;
            _aiCallRetryExecutor = aiCallRetryExecutor;
            _unitOfWork = unitOfWork;
        }

        public async Task<FollowUpThreadResult> Handle(CreateFollowUpThreadCommand request, CancellationToken cancellationToken)
        {
            _ = await _examTypeRepository.GetByIdAsync(request.ExamTypeId, cancellationToken)
                ?? throw new NotFoundException(nameof(ExamType), request.ExamTypeId);

            _ = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
                ?? throw new NotFoundException(nameof(User), request.UserId);

            var submission = await _submissionRepository.GetByIdAsync(request.SubmissionId, cancellationToken)
                ?? throw new NotFoundException(nameof(Submission), request.SubmissionId);

            var question = await _questionRepository.GetByIdAsync(submission.QuestionId, cancellationToken)
                ?? throw new NotFoundException(nameof(Question), submission.QuestionId);

            if (await _followUpThreadRepository.HasOpenThreadForSubmissionAsync(submission.Id, cancellationToken))
            {
                throw new ConflictException($"Submission '{submission.Id}' already has an open follow-up thread — close it before starting another.");
            }

            // Only legal from Graded — throws 409 if the submission is still being graded,
            // failed, archived, etc. Held under_dispute for the thread lifetime; a prior closed
            // thread (whatever its verdict) always ends the submission back at Graded, so
            // starting another thread from here works.
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

            var context = await FollowUpThreadSupport.LoadContextAsync(
                request.ExamTypeId, submission, question,
                _assessmentDimensionRepository, _errorTaxonomyRepository, _submissionRepository, _referenceTranslationRepository,
                cancellationToken);

            var askedAt = DateTimeOffset.UtcNow;

            FollowUpTurnPayload payload;
            try
            {
                var model = FollowUpThreadSupport.BuildTemplateModel(
                    request.QuestionText, request.ContextRef, submission, question, context, history: []);
                var prompt = await _examConfigLoader.BuildPromptAsync(request.ExamTypeId, AiOperationType.followup, model, cancellationToken);

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
                // No thread was ever created for this attempt — nothing justifies holding
                // under_dispute, so undo it and let a retry start clean from Graded.
                await FailAsync(submission, aiCallLog, $"Failed after {aiCallLog.AttemptCount} attempt(s): {ex.Message}", cancellationToken);
                throw new AiCallFailedException($"Follow-up thread could not be started: {ex.Message}", ex);
            }

            try
            {
                var thread = new FollowUpThread
                {
                    Id = Guid.NewGuid(),
                    SubmissionId = submission.Id,
                    UserId = request.UserId,
                    ExamTypeId = request.ExamTypeId,
                    ContextRef = request.ContextRef,
                    Status = FollowUpThreadStatus.open,
                    CreatedAt = askedAt,
                };
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

                await _followUpThreadRepository.AddAsync(thread, cancellationToken);
                await _followUpThreadRepository.AddMessageAsync(userMessage, cancellationToken);
                await _followUpThreadRepository.AddMessageAsync(aiMessage, cancellationToken);

                aiCallLog.Status = CallStatus.success;
                aiCallLog.RelatedId = thread.Id;
                aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return FollowUpThreadResult.From(thread, submission.Status, standardOverrideStatus: null);
            }
            catch (Exception ex)
            {
                await FailAsync(submission, aiCallLog, $"Failed to persist follow-up thread: {ex.Message}", cancellationToken);
                throw new AiCallFailedException($"Follow-up thread could not be started: {ex.Message}", ex);
            }
        }

        private async Task FailAsync(Submission submission, AiCallLog aiCallLog, string errorMessage, CancellationToken cancellationToken)
        {
            submission.TransitionTo(SubmissionStatus.graded);
            aiCallLog.Status = CallStatus.final_failure;
            aiCallLog.LastErrorMessage = errorMessage;
            aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
