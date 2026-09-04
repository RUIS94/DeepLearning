using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.Submissions.Queries.WaitForGradingStatus
{
    /// <summary>
    /// Long-poll for the end of a grading run: returns as soon as the submission leaves an
    /// in-progress status, or after <paramref name="WaitSeconds"/>, whichever comes first.
    /// </summary>
    public record WaitForGradingStatusQuery(Guid SubmissionId, int WaitSeconds)
        : IRequest<WaitForGradingStatusResult>;

    /// <param name="Terminal">
    /// True when grading is over one way or the other, so the client can stop watching and go
    /// read the full submission.
    /// </param>
    public record WaitForGradingStatusResult(Guid SubmissionId, SubmissionStatus Status, bool Terminal);
}
