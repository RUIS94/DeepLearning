using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.Submissions.Queries.WaitForGradingStatus
{
    /// <summary>
    /// Holds the request open until a grading run finishes, so the browser learns the moment it
    /// is over without asking every few seconds.
    ///
    /// <para>Grading takes minutes and the client has nothing useful to do until it ends. Plain
    /// polling forces a choice between a wasteful request rate and a laggy result — at a 30-second
    /// interval the user stares at a spinner for up to half a minute after the work is already
    /// done. Long-polling collapses that: one request per <see cref="MaxWaitSeconds"/>-ish window,
    /// and it returns within <see cref="PollInterval"/> of the status actually changing.</para>
    ///
    /// <para>Server-sent events would push instead of hold, but they need streaming all the way
    /// through the frontend's proxy route plus reconnect handling on the client, for a single
    /// event per grading. This is the cheaper shape for the same result.</para>
    ///
    /// <para>The wait is asynchronous — no thread is parked — and it honours the request's
    /// cancellation token, which is correct here: if the browser has gone away there is nothing
    /// left to tell.</para>
    /// </summary>
    public class WaitForGradingStatusQueryHandler
        : IRequestHandler<WaitForGradingStatusQuery, WaitForGradingStatusResult>
    {
        /// <summary>Ceiling on how long one request may hang, well inside the proxy's own 300s limit.</summary>
        public const int MaxWaitSeconds = 60;

        /// <summary>How often the held request re-reads the status. One indexed single-row read.</summary>
        public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

        private readonly ISubmissionRepository _submissionRepository;

        public WaitForGradingStatusQueryHandler(ISubmissionRepository submissionRepository)
        {
            _submissionRepository = submissionRepository;
        }

        /// <summary>Statuses that mean "a grading run is under way, keep waiting".</summary>
        private static bool IsInProgress(SubmissionStatus status)
            => status is SubmissionStatus.submitted or SubmissionStatus.grading;

        public async Task<WaitForGradingStatusResult> Handle(
            WaitForGradingStatusQuery request, CancellationToken cancellationToken)
        {
            var deadline = DateTimeOffset.UtcNow
                + TimeSpan.FromSeconds(Math.Clamp(request.WaitSeconds, 0, MaxWaitSeconds));

            while (true)
            {
                var status = await _submissionRepository.GetStatusAsync(request.SubmissionId, cancellationToken)
                    ?? throw new NotFoundException(nameof(Submission), request.SubmissionId);

                if (!IsInProgress(status))
                {
                    return new WaitForGradingStatusResult(request.SubmissionId, status, true);
                }

                if (DateTimeOffset.UtcNow >= deadline)
                {
                    // Still running. The client re-issues and keeps its spinner up — it never
                    // re-triggers the grading itself.
                    return new WaitForGradingStatusResult(request.SubmissionId, status, false);
                }

                await Task.Delay(PollInterval, cancellationToken);
            }
        }
    }
}
