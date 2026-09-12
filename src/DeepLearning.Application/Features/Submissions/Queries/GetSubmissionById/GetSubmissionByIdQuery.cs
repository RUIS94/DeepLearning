using MediatR;

namespace DeepLearning.Application.Features.Submissions.Queries.GetSubmissionById
{
    /// <summary>
    /// RequesterId gates access (ref/管理员与用户权限隔离_策划书.md U6): a submission not owned by
    /// RequesterId 404s rather than revealing it exists but isn't theirs.
    /// </summary>
    public record GetSubmissionByIdQuery(Guid Id, Guid RequesterId) : IRequest<GetSubmissionByIdResult>;
}
