using MediatR;

namespace DeepLearning.Application.Features.FollowUpThreads.Queries.ListFollowUpThreadsBySubmission
{
    /// <summary>
    /// All follow-up threads for a submission, newest first. Empty list (not 404) when there are
    /// none — but a submission that exists and isn't owned by RequesterId still 404s
    /// (ref/管理员与用户权限隔离_策划书.md U6), same "don't reveal it exists" convention as
    /// GetSubmissionById/GetQuestionById.
    /// </summary>
    public record ListFollowUpThreadsBySubmissionQuery(Guid SubmissionId, Guid RequesterId) : IRequest<List<FollowUpThreadSummary>>;
}
