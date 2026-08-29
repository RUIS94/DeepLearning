using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace DeepLearning.Application.Features.Submissions.Commands.CreateSubmission
{
    public class CreateSubmissionCommandHandler : IRequestHandler<CreateSubmissionCommand, CreateSubmissionResult>
    {
        private readonly IQuestionRepository _questionRepository;
        private readonly IUserRepository _userRepository;
        private readonly ISubmissionRepository _submissionRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateSubmissionCommandHandler(
            IQuestionRepository questionRepository,
            IUserRepository userRepository,
            ISubmissionRepository submissionRepository,
            IUnitOfWork unitOfWork)
        {
            _questionRepository = questionRepository;
            _userRepository = userRepository;
            _submissionRepository = submissionRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<CreateSubmissionResult> Handle(CreateSubmissionCommand request, CancellationToken cancellationToken)
        {
            var question = await _questionRepository.GetByIdAsync(request.QuestionId, cancellationToken)
                ?? throw new NotFoundException(nameof(Question), request.QuestionId);

            _ = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
                ?? throw new NotFoundException(nameof(User), request.UserId);

            if (question.TaskType != request.TaskType)
            {
                throw new ValidationException(new[]
                {
                    new ValidationFailure(
                        nameof(CreateSubmissionCommand.TaskType),
                        $"Question '{request.QuestionId}' is a TaskType.{question.TaskType} question, cannot submit as TaskType.{request.TaskType}."),
                });
            }

            var submission = new Submission
            {
                Id = Guid.NewGuid(),
                QuestionId = request.QuestionId,
                UserId = request.UserId,
                TaskType = request.TaskType,
                Content = request.Content,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            submission.TransitionTo(SubmissionStatus.submitted);
            submission.SubmittedAt = DateTimeOffset.UtcNow;

            await _submissionRepository.AddAsync(submission, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new CreateSubmissionResult(submission.Id, submission.QuestionId, submission.TaskType, submission.Status, submission.SubmittedAt);
        }
    }
}
