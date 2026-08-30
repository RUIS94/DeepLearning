using MediatR;

namespace DeepLearning.Application.Features.FollowUps.Queries.GetFollowUpQuestionById
{
    public record GetFollowUpQuestionByIdQuery(Guid Id) : IRequest<GetFollowUpQuestionByIdResult>;
}
