using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeepLearning.UnitTests.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// <see cref="RubricCalibrationReportJob"/> is the §10.6 / Step 10 calibration digest. The
    /// skeleton just windows the active overrides and logs them, so the tests pin exactly that:
    /// only rows that became active inside the lookback window are reported, and an empty window
    /// is a clean no-op. Pure hand-rolled fake, no DB — same convention as ProgressSnapshotJobTests.
    /// </summary>
    public class RubricCalibrationReportJobTests
    {
        private sealed class FakeStandardOverrideRepository : IStandardOverrideRepository
        {
            private readonly List<StandardOverride> _all;
            public FakeStandardOverrideRepository(List<StandardOverride> all) => _all = all;

            public Task<List<StandardOverride>> ListAsync(
                OverrideStatus? status, Guid? examTypeId = null, CancellationToken cancellationToken = default)
                => Task.FromResult(_all.Where(o => status is null || o.Status == status).ToList());

            public Task<StandardOverride?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();
            public Task<StandardOverride?> GetActiveByRuleAsync(OverrideScope scope, string dimensionOrRule, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();
            public Task<int> CountDistinctQuestionsPendingAsync(OverrideScope scope, string dimensionOrRule, Guid? baselineOverrideId, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();
            public Task<List<StandardOverride>> ListActiveByExamTypeAsync(Guid examTypeId, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();
            public Task AddAsync(StandardOverride standardOverride, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();
        }

        private static StandardOverride Active(DateTimeOffset effectiveFrom) => new()
        {
            Id = Guid.NewGuid(),
            Scope = OverrideScope.grading_rubric,
            DimensionOrRule = "meaning_transfer",
            RevisedRuleText = "revised text",
            Status = OverrideStatus.active,
            EffectiveFrom = effectiveFrom,
            CreatedAt = effectiveFrom,
        };

        private static RubricCalibrationReportJob NewJob(params StandardOverride[] rows)
            => new(new FakeStandardOverrideRepository(rows.ToList()), NullLogger<RubricCalibrationReportJob>.Instance);

        [Fact]
        public async Task Reports_nothing_when_no_override_became_active_in_the_window()
        {
            var stale = Active(DateTimeOffset.UtcNow - TimeSpan.FromDays(RubricCalibrationReportJob.LookbackDays + 5));

            var reported = await NewJob(stale).RunAsync(CancellationToken.None);

            Assert.Equal(0, reported);
        }

        [Fact]
        public async Task Reports_only_the_active_rows_whose_effective_date_is_inside_the_window()
        {
            var recent = Active(DateTimeOffset.UtcNow - TimeSpan.FromDays(2));
            var alsoRecent = Active(DateTimeOffset.UtcNow - TimeSpan.FromHours(1));
            var justOutside = Active(DateTimeOffset.UtcNow - TimeSpan.FromDays(RubricCalibrationReportJob.LookbackDays + 1));
            var observingInWindow = new StandardOverride
            {
                Id = Guid.NewGuid(),
                Scope = OverrideScope.grading_rubric,
                DimensionOrRule = "textual_norms",
                RevisedRuleText = "not active yet",
                Status = OverrideStatus.observing,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            var reported = await NewJob(recent, alsoRecent, justOutside, observingInWindow).RunAsync(CancellationToken.None);

            Assert.Equal(2, reported);
        }
    }
}
