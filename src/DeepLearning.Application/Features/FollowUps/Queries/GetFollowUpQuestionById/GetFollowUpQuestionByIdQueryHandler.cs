using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.FollowUps.Queries.GetFollowUpQuestionById
{
    public class GetFollowUpQuestionByIdQueryHandler : IRequestHandler<GetFollowUpQuestionByIdQuery, GetFollowUpQuestionByIdResult>
    {
        private readonly IFollowUpQuestionRepository _followUpQuestionRepository;

        public GetFollowUpQuestionByIdQueryHandler(IFollowUpQuestionRepository followUpQuestionRepository)
        {
            _followUpQuestionRepository = followUpQuestionRepository;
        }

        public async Task<GetFollowUpQuestionByIdResult> Handle(GetFollowUpQuestionByIdQuery request, CancellationToken cancellationToken)
        {
            var followUp = await _followUpQuestionRepository.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(FollowUpQuestion), request.Id);

            return new GetFollowUpQuestionByIdResult(
                followUp.Id,
                followUp.SubmissionId,
                followUp.UserId,
                followUp.ContextRef,
                followUp.QuestionText,
                followUp.AiResponse,
                followUp.Verdict,
                followUp.CreatedAt);
        }
    }
}
