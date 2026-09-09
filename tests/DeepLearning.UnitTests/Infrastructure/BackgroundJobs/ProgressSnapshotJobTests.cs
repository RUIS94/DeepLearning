using DeepLearning.Application.Features.Progress.Commands.GenerateProgressTrendSnapshot;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.BackgroundJobs;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeepLearning.UnitTests.Infrastructure.BackgroundJobs
{
    public class ProgressSnapshotJobTests
    {
        private class FakeMediator : IMediator
        {
            public List<object> SentRequests { get; } = [];

            public Func<object, bool>? ThrowFor { get; set; }

            public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            {
                SentRequests.Add(request);
                if (ThrowFor?.Invoke(request) == true)
                {
                    throw new InvalidOperationException("Simulated failure for this unit.");
                }

                return Task.FromResult(default(TResponse)!);
            }

            public Task<object?> Send(object request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest => throw new NotImplementedException();

            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

            public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
                where TNotification : INotification
                => Task.CompletedTask;
        }

        private class FakeExamTypeRepository : IExamTypeRepository
        {
            private readonly List<ExamType> _examTypes;

            public FakeExamTypeRepository(List<ExamType> examTypes)
            {
                _examTypes = examTypes;
            }

            public Task<ExamType?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
                => Task.FromResult(_examTypes.SingleOrDefault(x => x.Id == id));

            public Task<ExamType?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
                => Task.FromResult(_examTypes.SingleOrDefault(x => x.Code == code));

            public Task<List<ExamType>> ListAsync(bool? isActive, CancellationToken cancellationToken = default)
                => Task.FromResult(isActive is null ? _examTypes : _examTypes.Where(x => x.IsActive == isActive).ToList());

            public Task AddAsync(ExamType examType, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        private class FakeProgressRepository : IProgressRepository
        {
            private readonly List<Guid> _userIds;

            public FakeProgressRepository(List<Guid> userIds)
            {
                _userIds = userIds;
            }

            public Task<List<Guid>> ListUserIdsWithGradingActivitySinceAsync(DateOnly since, CancellationToken cancellationToken = default)
                => Task.FromResult(_userIds);

            public Task<ProgressSnapshot?> GetByUserPeriodAsync(Guid userId, DateOnly periodStart, DateOnly periodEnd, string? difficultyTier, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            public Task<List<GradingResult>> GetGradingResultsForUserInPeriodAsync(Guid userId, string? difficultyTier, DateOnly periodStart, DateOnly periodEnd, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            public Task AddAsync(ProgressSnapshot snapshot, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            public Task<List<ProgressSnapshot>> ListByUserAsync(Guid userId, string? difficultyTier, CancellationToken cancellationToken = default) => throw new NotImplementedException();

            public Task<List<ProgressSnapshot>> ListRecentBeforeAsync(Guid userId, string difficultyTier, DateOnly beforePeriodStart, int take, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        }

        private static ExamType NewExamType() => new()
        {
            Id = Guid.NewGuid(),
            Code = $"test_{Guid.NewGuid():N}",
            Name = "Test Exam Type",
            SubjectCategory = SubjectCategory.translation,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        [Fact]
        public async Task Sends_one_command_per_user_times_difficulty_tier_times_trailing_week()
        {
            var examType = NewExamType();
            var userIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            var mediator = new FakeMediator();

            var job = new ProgressSnapshotJob(
                new FakeExamTypeRepository([examType]),
                new FakeProgressRepository(userIds),
                mediator,
                NullLogger<ProgressSnapshotJob>.Instance);

            await job.RunAsync(CancellationToken.None);

            // 2 users x 3 difficulty tiers x 12 trailing weeks (LookbackWeeks) — one exam type,
            // so no per-exam-type multiplier.
            Assert.Equal(2 * 3 * 12, mediator.SentRequests.Count);
            Assert.All(mediator.SentRequests, r => Assert.IsType<GenerateProgressTrendSnapshotCommand>(r));
            Assert.All(
                mediator.SentRequests.Cast<GenerateProgressTrendSnapshotCommand>(),
                c => Assert.Equal(examType.Id, c.ExamTypeId));

            var forFirstUser = mediator.SentRequests
                .Cast<GenerateProgressTrendSnapshotCommand>()
                .Where(c => c.UserId == userIds[0])
                .ToList();
            Assert.Equal(3 * 12, forFirstUser.Count);
            Assert.Contains(forFirstUser, c => c.DifficultyTier == "easy");
            Assert.Contains(forFirstUser, c => c.DifficultyTier == "medium");
            Assert.Contains(forFirstUser, c => c.DifficultyTier == "hard");
        }

        [Fact]
        public async Task Multiple_active_exam_types_still_narrate_against_exactly_one_the_earliest_created()
        {
            // progress_snapshots has no exam_type_id, so the job must not fan out over every
            // active exam type (that silently made whichever ran last win). It narrates against
            // one — the earliest-created — regardless of how many are active.
            var older = NewExamType();
            older.CreatedAt = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var newer = NewExamType();
            newer.CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

            var userIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            var mediator = new FakeMediator();

            var job = new ProgressSnapshotJob(
                new FakeExamTypeRepository([newer, older]),
                new FakeProgressRepository(userIds),
                mediator,
                NullLogger<ProgressSnapshotJob>.Instance);

            await job.RunAsync(CancellationToken.None);

            // Still 2 users x 3 tiers x 12 weeks — NOT doubled by the second exam type.
            Assert.Equal(2 * 3 * 12, mediator.SentRequests.Count);
            Assert.All(
                mediator.SentRequests.Cast<GenerateProgressTrendSnapshotCommand>(),
                c => Assert.Equal(older.Id, c.ExamTypeId));
        }

        [Fact]
        public async Task One_failing_unit_does_not_stop_the_rest_of_the_batch()
        {
            var examType = NewExamType();
            var userIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            var mediator = new FakeMediator
            {
                ThrowFor = req => req is GenerateProgressTrendSnapshotCommand c && c.UserId == userIds[0],
            };

            var job = new ProgressSnapshotJob(
                new FakeExamTypeRepository([examType]),
                new FakeProgressRepository(userIds),
                mediator,
                NullLogger<ProgressSnapshotJob>.Instance);

            // Every unit for userIds[0] throws, but the job must still process userIds[1]'s units
            // in full rather than aborting the whole batch.
            await job.RunAsync(CancellationToken.None);

            var forSecondUser = mediator.SentRequests
                .Cast<GenerateProgressTrendSnapshotCommand>()
                .Count(c => c.UserId == userIds[1]);
            Assert.Equal(3 * 12, forSecondUser);
        }

        [Fact]
        public async Task No_active_exam_types_sends_no_commands()
        {
            var mediator = new FakeMediator();
            var job = new ProgressSnapshotJob(
                new FakeExamTypeRepository([]),
                new FakeProgressRepository([Guid.NewGuid()]),
                mediator,
                NullLogger<ProgressSnapshotJob>.Instance);

            await job.RunAsync(CancellationToken.None);

            Assert.Empty(mediator.SentRequests);
        }
    }
}
