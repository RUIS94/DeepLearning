using DeepLearning.Application.Features.Submissions.Commands.GradeSubmission;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Exceptions;
using Hangfire;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DeepLearning.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Runs one grading off the request thread. Deliberately thin — pure dispatch, no business
    /// logic — matching this codebase's "a scheduled job is just a different trigger for the same
    /// CQRS command" convention (see ProgressSnapshotJob).
    ///
    /// <para><b>No automatic retries.</b> GradeSubmissionCommandHandler already owns the failure
    /// policy: it re-prompts a stage up to three times, and on final failure it puts the
    /// submission into GradingFailed, which the user can retry from the UI. Hangfire's default of
    /// ten more attempts on top of that would silently re-run a four-call grading — real money —
    /// against a submission nobody is waiting on, and would keep flipping a row the user has
    /// already been told failed.</para>
    /// </summary>
    [AutomaticRetry(Attempts = 0)]
    public class GradeSubmissionJob
    {
        private readonly IMediator _mediator;
        private readonly ILogger<GradeSubmissionJob> _logger;

        public GradeSubmissionJob(IMediator mediator, ILogger<GradeSubmissionJob> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        public async Task RunAsync(Guid submissionId, Guid examTypeId, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _mediator.Send(new GradeSubmissionCommand(submissionId, examTypeId), cancellationToken);
                _logger.LogInformation(
                    "Graded submission {SubmissionId}: {DimensionCount} dimension(s), {ErrorCount} error(s).",
                    submissionId,
                    result.GradingResultCount,
                    result.ErrorListCount);
            }
            catch (Exception ex) when (ex is AiCallFailedException or InvalidSubmissionStateException or ConflictException)
            {
                // Already reflected in the submission's own status (GradingFailed, or unchanged
                // because another run got there first), which is what the polling client reads.
                // Letting it escape would only mark the Hangfire job failed for a state the
                // system already handled and recorded.
                _logger.LogWarning(ex, "Grading submission {SubmissionId} did not complete.", submissionId);
            }
        }
    }

    /// <summary>Production <see cref="IGradingJobQueue"/>: hands the run to Hangfire.</summary>
    public class HangfireGradingJobQueue : IGradingJobQueue
    {
        private readonly IBackgroundJobClient _backgroundJobs;

        public HangfireGradingJobQueue(IBackgroundJobClient backgroundJobs)
        {
            _backgroundJobs = backgroundJobs;
        }

        public Task EnqueueAsync(Guid submissionId, Guid examTypeId, CancellationToken cancellationToken = default)
        {
            // CancellationToken.None, not the request's: the whole point is to outlive the
            // request. Hangfire supplies its own shutdown token at execution time.
            _backgroundJobs.Enqueue<GradeSubmissionJob>(job => job.RunAsync(submissionId, examTypeId, CancellationToken.None));
            return Task.CompletedTask;
        }
    }
}
