using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.BackgroundJobs;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeepLearning.UnitTests.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Weak-point extraction runs after grading rather than inside it, so its progress is the
    /// only thing the learner can see about it. These tests pin that the status always ends up
    /// somewhere final — in particular that a failure is recorded rather than leaving the
    /// submission reading "running" forever, which would give the UI nothing to offer a retry on.
    /// </summary>
    public class GenerateWeakPointsJobTests
    {
        private sealed class ThrowingMediator : IMediator
        {
            private readonly Exception? _throw;

            public ThrowingMediator(Exception? toThrow = null) => _throw = toThrow;

            public int Sends { get; private set; }

            public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
                where TRequest : IRequest
            {
                Sends++;
                return _throw is null ? Task.CompletedTask : Task.FromException(_throw);
            }

            public Task<object?> Send(object request, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
                where TNotification : INotification => Task.CompletedTask;

            public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }

        /// <summary>Holds one submission in memory and records the statuses written to it, in order.</summary>
        private sealed class RecordingSubmissionRepository : ISubmissionRepository
        {
            private readonly Submission _submission;

            public RecordingSubmissionRepository(Submission submission) => _submission = submission;

            public List<WeakPointGenerationStatus?> Written { get; } = [];

            public void Capture() => Written.Add(_submission.WeakPointGenerationStatus);

            public Task<Submission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
                => Task.FromResult<Submission?>(_submission.Id == id ? _submission : null);

            public Task<SubmissionStatus?> GetStatusAsync(Guid id, CancellationToken cancellationToken = default)
                => Task.FromResult<SubmissionStatus?>(_submission.Status);

            public Task<List<Submission>> ListByUserAsync(Guid userId, Guid? questionId, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<List<DateTimeOffset>> ListRecentGradedCreatedAtAsync(Guid userId, int count, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<List<GradingResult>> GetGradingResultsAsync(Guid submissionId, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<List<ErrorListItem>> GetErrorListAsync(Guid submissionId, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task AddAsync(Submission submission, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task AddGradingResultsAsync(IEnumerable<GradingResult> results, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task AddErrorListItemsAsync(IEnumerable<ErrorListItem> items, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        /// <summary>Each save is the moment a status becomes visible, so that is when we record it.</summary>
        private sealed class CapturingUnitOfWork : IUnitOfWork
        {
            private readonly RecordingSubmissionRepository _repository;

            public CapturingUnitOfWork(RecordingSubmissionRepository repository) => _repository = repository;

            public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            {
                _repository.Capture();
                return Task.FromResult(1);
            }
        }

        private static (GenerateWeakPointsJob Job, Submission Submission, RecordingSubmissionRepository Repository) Build(
            Exception? extractionFailure)
        {
            var submission = new Submission
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                QuestionId = Guid.NewGuid(),
                Status = SubmissionStatus.graded,
                Content = "\"译文\"",
                WeakPointGenerationStatus = WeakPointGenerationStatus.pending,
            };
            var repository = new RecordingSubmissionRepository(submission);
            var job = new GenerateWeakPointsJob(
                new ThrowingMediator(extractionFailure),
                repository,
                new CapturingUnitOfWork(repository),
                NullLogger<GenerateWeakPointsJob>.Instance);

            return (job, submission, repository);
        }

        [Fact]
        public async Task A_successful_run_goes_pending_to_running_to_succeeded()
        {
            var (job, submission, repository) = Build(extractionFailure: null);

            await job.RunAsync(submission.Id, submission.UserId, Guid.NewGuid());

            Assert.Equal(
                [WeakPointGenerationStatus.running, WeakPointGenerationStatus.succeeded],
                repository.Written);
            Assert.Equal(WeakPointGenerationStatus.succeeded, submission.WeakPointGenerationStatus);
        }

        [Fact]
        public async Task A_failed_run_is_recorded_rather_than_left_running()
        {
            // Without this the submission reads "running" forever and the UI has nothing to hang
            // a retry on — the learner would just watch a spinner that never resolves.
            var (job, submission, repository) = Build(new InvalidOperationException("classifier exploded"));

            await job.RunAsync(submission.Id, submission.UserId, Guid.NewGuid());

            Assert.Equal(
                [WeakPointGenerationStatus.running, WeakPointGenerationStatus.failed],
                repository.Written);
            Assert.Equal(WeakPointGenerationStatus.failed, submission.WeakPointGenerationStatus);
        }

        [Fact]
        public async Task A_failure_never_escapes_the_job()
        {
            // It must not surface as a Hangfire job failure either: the outcome is already on the
            // submission, and the job is configured not to retry.
            var (job, submission, _) = Build(new InvalidOperationException("classifier exploded"));

            var thrown = await Record.ExceptionAsync(() => job.RunAsync(submission.Id, submission.UserId, Guid.NewGuid()));

            Assert.Null(thrown);
        }

        [Fact]
        public async Task A_cancelled_run_still_records_the_failure()
        {
            // AGENTS.md #13: the token being cancelled is often exactly why we are in the failure
            // path, so the write that records it must not depend on that same token.
            var (job, submission, repository) = Build(new OperationCanceledException());
            using var alreadyCancelled = new CancellationTokenSource();
            await alreadyCancelled.CancelAsync();

            await job.RunAsync(submission.Id, submission.UserId, Guid.NewGuid(), alreadyCancelled.Token);

            Assert.Equal(WeakPointGenerationStatus.failed, submission.WeakPointGenerationStatus);
        }
    }
}
