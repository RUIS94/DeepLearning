using MediatR;

namespace DeepLearning.Application.Features.FollowUpThreads.Queries.GetFollowUpThreadById
{
    /// <summary>Full thread with its messages. Throws NotFoundException (404) if the id is unknown.</summary>
    public record GetFollowUpThreadByIdQuery(Guid Id) : IRequest<FollowUpThreadResult>;
}
