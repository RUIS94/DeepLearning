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
    }
}
