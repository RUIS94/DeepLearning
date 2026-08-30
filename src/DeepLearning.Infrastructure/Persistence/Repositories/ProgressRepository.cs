using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class ProgressRepository : IProgressRepository
    {
        private readonly AppDbContext _context;

        public ProgressRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<ProgressSnapshot?> GetByUserPeriodAsync(
            Guid userId,
            DateOnly periodStart,
            DateOnly periodEnd,
            string? difficultyTier,
            CancellationToken cancellationToken = default)
            => _context.ProgressSnapshots.FirstOrDefaultAsync(
                x => x.UserId == userId
                    && x.PeriodStart == periodStart
                    && x.PeriodEnd == periodEnd
                    && x.DifficultyTier == difficultyTier,
                cancellationToken);

        public Task<List<GradingResult>> GetGradingResultsForUserInPeriodAsync(
            Guid userId,
            string? difficultyTier,
            DateOnly periodStart,
            DateOnly periodEnd,
            CancellationToken cancellationToken = default)
        {
            var periodStartUtc = periodStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var periodEndUtc = periodEnd.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

            return _context.GradingResults
                .Include(x => x.Submission)
                    .ThenInclude(s => s!.Question)
                .Include(x => x.Dimension)
                .Where(x => x.Submission!.UserId == userId
                    && (x.Submission!.Status == Domain.Enums.SubmissionStatus.graded || x.Submission!.Status == Domain.Enums.SubmissionStatus.archived)
                    && x.Submission!.UpdatedAt >= periodStartUtc
                    && x.Submission!.UpdatedAt <= periodEndUtc
                    && (difficultyTier == null || x.Submission!.Question!.Difficulty.ToString() == difficultyTier))
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(ProgressSnapshot snapshot, CancellationToken cancellationToken = default)
            => await _context.ProgressSnapshots.AddAsync(snapshot, cancellationToken);

        public Task<List<ProgressSnapshot>> ListByUserAsync(
            Guid userId,
            string? difficultyTier,
            CancellationToken cancellationToken = default)
            => _context.ProgressSnapshots
                .Where(x => x.UserId == userId && (difficultyTier == null || x.DifficultyTier == difficultyTier))
                .OrderBy(x => x.PeriodStart)
                .ToListAsync(cancellationToken);

        public Task<List<ProgressSnapshot>> ListRecentBeforeAsync(
            Guid userId,
            string difficultyTier,
            DateOnly beforePeriodStart,
            int take,
            CancellationToken cancellationToken = default)
            => _context.ProgressSnapshots
                .Where(x => x.UserId == userId && x.DifficultyTier == difficultyTier && x.PeriodStart < beforePeriodStart)
                .OrderByDescending(x => x.PeriodStart)
                .Take(take)
                .ToListAsync(cancellationToken);

        public async Task<List<Guid>> ListUserIdsWithGradingActivitySinceAsync(
            DateOnly since,
            CancellationToken cancellationToken = default)
        {
            var sinceUtc = since.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

            return await _context.Submissions
                .Where(x => (x.Status == Domain.Enums.SubmissionStatus.graded || x.Status == Domain.Enums.SubmissionStatus.archived)
                    && x.UpdatedAt >= sinceUtc)
                .Select(x => x.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);
        }
    }
}
