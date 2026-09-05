using System.Diagnostics;
using DeepLearning.Application.Features.Submissions.Queries.WaitForGradingStatus;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Exceptions;

namespace DeepLearning.UnitTests.Application.Features.Submissions
{
    /// <summary>
    /// The grading long-poll. It exists because plain polling forces a choice between hammering
    /// the API and leaving the user watching a spinner after the work has already finished: at a
    /// 30-second interval the result is up to half a minute late, and at a 3-second interval the
    /// browser fires twenty requests a minute for a job that takes three.
    /// </summary>
    public class WaitForGradingStatusQueryHandlerTests
    {
        /// <summary>
        /// Hands back a scripted sequence of statuses, one per read, so a test can say "still
        /// grading, still grading, then done" without a database or a real clock.
        /// </summary>
        private sealed class ScriptedSubmissionRepository : ISubmissionRepository
        {
            private readonly Queue<SubmissionStatus?> _script;
            private readonly SubmissionStatus? _thereafter;

            public ScriptedSubmissionRepository(SubmissionStatus? thereafter, params SubmissionStatus?[] script)
            {
                _script = new Queue<SubmissionStatus?>(script);
                _thereafter = thereafter;
            }

            public int Reads { get; private set; }

            public Task<SubmissionStatus?> GetStatusAsync(Guid id, CancellationToken cancellationToken = default)
            {
                Reads++;
                return Task.FromResult(_script.Count > 0 ? _script.Dequeue() : _thereafter);
            }

            public Task<Submission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<SubmissionSourceAndTranslation?> GetSourceAndTranslationAsync(Guid submissionId, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

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

        private static WaitForGradingStatusQueryHandler HandlerFor(ISubmissionRepository repository) => new(repository);

        [Theory]
        [InlineData(SubmissionStatus.graded)]
        [InlineData(SubmissionStatus.grading_failed)]
        public async Task Returns_at_once_when_grading_is_already_over(SubmissionStatus status)
        {
            var repository = new ScriptedSubmissionRepository(status);
            var stopwatch = Stopwatch.StartNew();

            var result = await HandlerFor(repository).Handle(
                new WaitForGradingStatusQuery(Guid.NewGuid(), WaitSeconds: 30), CancellationToken.None);

            Assert.True(result.Terminal);
            Assert.Equal(status, result.Status);
            Assert.Equal(1, repository.Reads);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"should not have waited, took {stopwatch.Elapsed}");
        }

        [Fact]
        public async Task Returns_as_soon_as_the_status_changes_rather_than_at_the_deadline()
        {
            // Grading, grading, then done — the whole point is that the third read ends the wait
            // instead of the request sitting out its full budget.
            var repository = new ScriptedSubmissionRepository(
                SubmissionStatus.graded,
                SubmissionStatus.grading,
                SubmissionStatus.grading);
            var stopwatch = Stopwatch.StartNew();

            var result = await HandlerFor(repository).Handle(
                new WaitForGradingStatusQuery(Guid.NewGuid(), WaitSeconds: 60), CancellationToken.None);

            Assert.True(result.Terminal);
            Assert.Equal(SubmissionStatus.graded, result.Status);
            Assert.Equal(3, repository.Reads);
            // Two poll intervals, nowhere near the 60-second budget it was allowed.
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(20), $"waited far too long: {stopwatch.Elapsed}");
        }

        [Fact]
        public async Task Reports_not_terminal_when_the_budget_runs_out_with_grading_still_going()
        {
            var repository = new ScriptedSubmissionRepository(SubmissionStatus.grading);

            var result = await HandlerFor(repository).Handle(
                new WaitForGradingStatusQuery(Guid.NewGuid(), WaitSeconds: 0), CancellationToken.None);

            // The client re-issues and keeps its spinner up. It must never read this as a reason
            // to start the grading again — retries live entirely in the backend.
            Assert.False(result.Terminal);
            Assert.Equal(SubmissionStatus.grading, result.Status);
        }

        [Fact]
        public async Task Submitted_counts_as_in_progress_so_the_handoff_gap_does_not_end_the_wait()
        {
            // Between "queued" and the worker flipping the row to Grading there is a moment where
            // the status is still Submitted. Treating that as terminal would end the watch before
            // the run had even started.
            var repository = new ScriptedSubmissionRepository(SubmissionStatus.submitted);

            var result = await HandlerFor(repository).Handle(
                new WaitForGradingStatusQuery(Guid.NewGuid(), WaitSeconds: 0), CancellationToken.None);

            Assert.False(result.Terminal);
            Assert.Equal(SubmissionStatus.submitted, result.Status);
        }

        [Fact]
        public async Task An_unknown_submission_is_a_not_found_rather_than_an_endless_wait()
        {
            var repository = new ScriptedSubmissionRepository(thereafter: null);

            await Assert.ThrowsAsync<NotFoundException>(() => HandlerFor(repository).Handle(
                new WaitForGradingStatusQuery(Guid.NewGuid(), WaitSeconds: 30), CancellationToken.None));
        }

        [Fact]
        public async Task A_caller_supplied_budget_cannot_hold_the_connection_open_indefinitely()
        {
            // Nothing a client sends should be able to park a server connection for an hour.
            var repository = new ScriptedSubmissionRepository(SubmissionStatus.grading);
            var cancelAfterOneSecond = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => HandlerFor(repository).Handle(
                new WaitForGradingStatusQuery(Guid.NewGuid(), WaitSeconds: 3600), cancelAfterOneSecond.Token));

            Assert.True(WaitForGradingStatusQueryHandler.MaxWaitSeconds <= 60);
        }
    }
}
