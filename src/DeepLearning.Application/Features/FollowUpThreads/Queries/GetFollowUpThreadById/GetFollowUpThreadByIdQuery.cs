using MediatR;

namespace DeepLearning.Application.Features.FollowUpThreads.Queries.GetFollowUpThreadById
{
    /// <summary>
    /// Full thread with its messages. Throws NotFoundException (404) if the id is unknown, or if
    /// the thread's submission isn't owned by RequesterId (ref/管理员与用户权限隔离_策划书.md U6).
    /// </summary>
    public record GetFollowUpThreadByIdQuery(Guid Id, Guid RequesterId) : IRequest<FollowUpThreadResult>;
}
