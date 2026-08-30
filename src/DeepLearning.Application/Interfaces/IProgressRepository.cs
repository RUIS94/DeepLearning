using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    public interface IProgressRepository
    {
        Task<ProgressSnapshot?> GetByUserPeriodAsync(
            Guid userId,
            DateOnly periodStart,
            DateOnly periodEnd,
            string? difficultyTier,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Grading results for graded/archived submissions belonging to this user, within the
        /// given period (matched against Submission.UpdatedAt — the timestamp of its most recent
        /// state transition, which for a terminal graded/archived submission is when grading
        /// last actually finished) and optionally filtered to one Question.Difficulty tier.
        /// Includes Submission and Dimension so the caller can group by submission (for pass
        /// rate) and by dimension_key (for the per-dimension averages) without extra round trips.
        /// </summary>
        Task<List<GradingResult>> GetGradingResultsForUserInPeriodAsync(
            Guid userId,
            string? difficultyTier,
            DateOnly periodStart,
            DateOnly periodEnd,
            CancellationToken cancellationToken = default);

        Task AddAsync(ProgressSnapshot snapshot, CancellationToken cancellationToken = default);

        /// <summary>
        /// One row per snapshot the user has, oldest period first — the read side of
        /// GET /api/v1/progress (design doc §11.2 Step 9's "API测试(进度查询接口)"). Small,
        /// per-user dataset (at most one row per difficulty tier per period), so no paging.
        /// </summary>
        Task<List<ProgressSnapshot>> ListByUserAsync(
            Guid userId,
            string? difficultyTier,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Up to <paramref name="take"/> of this user+tier's most recent snapshots whose
        /// PeriodStart is strictly before <paramref name="beforePeriodStart"/>, most recent
        /// first — feeds GenerateProgressTrendSnapshotCommandHandler's AI call the trailing
        /// history it narrates a trend against, without pulling the user's entire snapshot
        /// history for what's meant to be a short "last few weeks" comparison.
        /// </summary>
        Task<List<ProgressSnapshot>> ListRecentBeforeAsync(
            Guid userId,
            string difficultyTier,
            DateOnly beforePeriodStart,
            int take,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Distinct ids of users with at least one graded/archived submission whose UpdatedAt
        /// falls on or after <paramref name="since"/> — ProgressSnapshotJob's iteration set, so
        /// the weekly job only does work for users who were actually active recently instead of
        /// scanning every registered user.
        /// </summary>
        Task<List<Guid>> ListUserIdsWithGradingActivitySinceAsync(
            DateOnly since,
            CancellationToken cancellationToken = default);
    }
}
