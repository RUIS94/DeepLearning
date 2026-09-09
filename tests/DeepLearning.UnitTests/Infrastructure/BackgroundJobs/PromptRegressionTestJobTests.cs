using DeepLearning.Application.Features.Submissions.Commands.GradeSubmission;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.BackgroundJobs;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeepLearning.UnitTests.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// <see cref="PromptRegressionTestJob"/> skeleton (design doc §7). The dry-run path is the
    /// only one exercised in CI: it must build the current Band histogram and must NOT send a
    /// single <see cref="GradeSubmissionCommand"/> (that's real LLM cost). Hand-rolled fakes, no
    /// DB — ProgressSnapshotJobTests convention.
    /// </summary>
    public class PromptRegressionTestJobTests
    {
        private sealed class FakeMediator : IMediator
        {
            public int GradeCommandsSent { get; private set; }

            public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            {
                if (request is GradeSubmissionCommand)
                {
                    GradeCommandsSent++;
                }

                return Task.FromResult(default(TResponse)!);
            }

            public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotImplementedException();
            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;
        }

        private sealed class FakeSubmissionRepository : ISubmissionRepository
        {
            private readonly List<Guid> _ids;
            private readonly Dictionary<Guid, List<GradingResult>> _results;

            public FakeSubmissionRepository(List<Guid> ids, Dictionary<Guid, List<GradingResult>> results)
            {
                _ids = ids;
                _results = results;
            }

            public Task<List<Guid>> ListRecentGradedIdsAsync(int take, CancellationToken cancellationToken = default)
                => Task.FromResult(_ids.Take(take).ToList());

            public Task<List<GradingResult>> GetGradingResultsAsync(Guid submissionId, CancellationToken cancellationToken = default)
                => Task.FromResult(_results.GetValueOrDefault(submissionId) ?? []);

            public Task<Submission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<SubmissionSourceAndTranslation?> GetSourceAndTranslationAsync(Guid submissionId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<SubmissionStatus?> GetStatusAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<List<Submission>> ListByUserAsync(Guid userId, Guid? questionId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<List<DateTimeOffset>> ListRecentGradedCreatedAtAsync(Guid userId, int count, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task<List<ErrorListItem>> GetErrorListAsync(Guid submissionId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task AddAsync(Submission submission, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task AddGradingResultsAsync(IEnumerable<GradingResult> results, CancellationToken cancellationToken = default) => throw new NotImplementedException();
            public Task AddErrorListItemsAsync(IEnumerable<ErrorListItem> items, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        private static GradingResult Result(string dimensionKey, int band) => new()
        {
            Id = Guid.NewGuid(),
            DimensionId = Guid.NewGuid(),
            Band = band,
            Dimension = new AssessmentDimension { Id = Guid.NewGuid(), DimensionKey = dimensionKey },
        };

        [Fact]
        public async Task Dry_run_builds_the_current_histogram_and_sends_no_grade_commands()
        {
            var s1 = Guid.NewGuid();
            var s2 = Guid.NewGuid();
            var mediator = new FakeMediator();
            var repo = new FakeSubmissionRepository(
                [s1, s2],
                new Dictionary<Guid, List<GradingResult>>
                {
                    [s1] = [Result("meaning_transfer", 2), Result("textual_norms", 3)],
                    [s2] = [Result("meaning_transfer", 2), Result("textual_norms", 4)],
                });

            var job = new PromptRegressionTestJob(repo, mediator, NullLogger<PromptRegressionTestJob>.Instance);

            var report = await job.RunAsync(Guid.NewGuid(), sampleSize: 10, dryRun: true);

            Assert.Equal(0, mediator.GradeCommandsSent);
            Assert.Equal(2, report.SampledCount);
            Assert.Null(report.After);
            Assert.False(report.DriftDetected);
            // meaning_transfer: two Band-2 -> counts[1] == 2
            Assert.Equal(new[] { 0, 2, 0, 0, 0 }, report.Before["meaning_transfer"]);
            // textual_norms: one Band-3, one Band-4
            Assert.Equal(new[] { 0, 0, 1, 1, 0 }, report.Before["textual_norms"]);
        }

        [Fact]
        public async Task Sample_size_caps_the_number_of_submissions_pulled()
        {
            var ids = Enumerable.Range(0, 50).Select(_ => Guid.NewGuid()).ToList();
            var results = ids.ToDictionary(id => id, id => (List<GradingResult>)[Result("meaning_transfer", 2)]);
            var mediator = new FakeMediator();
            var job = new PromptRegressionTestJob(
                new FakeSubmissionRepository(ids, results), mediator, NullLogger<PromptRegressionTestJob>.Instance);

            var report = await job.RunAsync(Guid.NewGuid(), sampleSize: 5, dryRun: true);

            Assert.Equal(5, report.SampledCount);
            Assert.Equal(0, mediator.GradeCommandsSent);
        }
    }
}
