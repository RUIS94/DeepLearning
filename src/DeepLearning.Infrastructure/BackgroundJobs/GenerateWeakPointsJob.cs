using DeepLearning.Application.Features.WeakPoints.Commands.GenerateWeakPoints;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Enums;
using Hangfire;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DeepLearning.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Runs weak-point extraction off the grading path and keeps
    /// Submission.WeakPointGenerationStatus in step, so the UI can show a tag instead of making
    /// the learner wait for work whose result they are not being shown yet.
    ///
    /// <para><b>No automatic retries.</b> Extraction makes an LLM call; ten silent re-runs of a
    /// failed one would spend real money on a submission nobody is waiting for. The failure is
    /// recorded on the submission and the learner can ask for it again — the same policy as
    /// grading itself (see GradeSubmissionJob).</para>
    /// </summary>
    [AutomaticRetry(Attempts = 0)]
    public class GenerateWeakPointsJob
    {
        private readonly IMediator _mediator;
        private readonly ISubmissionRepository _submissionRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GenerateWeakPointsJob> _logger;

        public GenerateWeakPointsJob(
            IMediator mediator,
            ISubmissionRepository submissionRepository,
            IUnitOfWork unitOfWork,
            ILogger<GenerateWeakPointsJob> logger)
        {
            _mediator = mediator;
            _submissionRepository = submissionRepository;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task RunAsync(Guid submissionId, Guid userId, Guid examTypeId, CancellationToken cancellationToken = default)
        {
            await SetStatusAsync(submissionId, WeakPointGenerationStatus.running, cancellationToken);

            try
            {
                await _mediator.Send(new GenerateWeakPointsCommand(submissionId, userId, examTypeId), cancellationToken);
                await SetStatusAsync(submissionId, WeakPointGenerationStatus.succeeded, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Weak-point extraction failed for submission {SubmissionId}.", submissionId);

                // CancellationToken.None, and outside the catch's own await chain: whatever went
                // wrong must still be recorded, or the submission is left reading "running"
                // forever and the learner has no way to ask for it again (AGENTS.md #13).
                await SetStatusAsync(submissionId, WeakPointGenerationStatus.failed, CancellationToken.None);
            }
        }

        private async Task SetStatusAsync(Guid submissionId, WeakPointGenerationStatus status, CancellationToken cancellationToken)
        {
            var submission = await _submissionRepository.GetByIdAsync(submissionId, cancellationToken);
            if (submission is null)
            {
                return;
            }

            submission.WeakPointGenerationStatus = status;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>Production <see cref="IWeakPointGenerationQueue"/>: hands the run to Hangfire.</summary>
    public class HangfireWeakPointGenerationQueue : IWeakPointGenerationQueue
    {
        private readonly IBackgroundJobClient _backgroundJobs;

        public HangfireWeakPointGenerationQueue(IBackgroundJobClient backgroundJobs)
        {
            _backgroundJobs = backgroundJobs;
        }

        public Task EnqueueAsync(Guid submissionId, Guid userId, Guid examTypeId, CancellationToken cancellationToken = default)
        {
            _backgroundJobs.Enqueue<GenerateWeakPointsJob>(
                job => job.RunAsync(submissionId, userId, examTypeId, CancellationToken.None));
            return Task.CompletedTask;
        }
    }
}
