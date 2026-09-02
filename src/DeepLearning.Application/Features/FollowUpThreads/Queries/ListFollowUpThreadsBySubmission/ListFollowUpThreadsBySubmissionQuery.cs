using MediatR;

namespace DeepLearning.Application.Features.FollowUpThreads.Queries.ListFollowUpThreadsBySubmission
{
    /// <summary>All follow-up threads for a submission, newest first. Empty list (not 404) when there are none.</summary>
    public record ListFollowUpThreadsBySubmissionQuery(Guid SubmissionId) : IRequest<List<FollowUpThreadSummary>>;
}
