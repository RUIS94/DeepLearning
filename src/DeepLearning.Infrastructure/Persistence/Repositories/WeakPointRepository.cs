using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class WeakPointRepository : IWeakPointRepository
    {
        private readonly AppDbContext _context;

        public WeakPointRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<WeakPoint?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.WeakPoints.Include(x => x.Catalog).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public Task<WeakPoint?> GetByUserAndCategoryAsync(Guid userId, string category, CancellationToken cancellationToken = default)
            => _context.WeakPoints.FirstOrDefaultAsync(x => x.UserId == userId && x.Category == category, cancellationToken);

        public Task<List<WeakPoint>> ListByCatalogIdAsync(Guid catalogId, CancellationToken cancellationToken = default)
            => _context.WeakPoints.Where(x => x.CatalogId == catalogId).ToListAsync(cancellationToken);

        public Task<List<WeakPointOccurrence>> ListOccurrencesByWeakPointAsync(Guid weakPointId, CancellationToken cancellationToken = default)
            => _context.WeakPointOccurrences.Where(x => x.WeakPointId == weakPointId).ToListAsync(cancellationToken);

        public void RemoveWeakPoint(WeakPoint weakPoint) => _context.WeakPoints.Remove(weakPoint);

        public void RemoveOccurrence(WeakPointOccurrence occurrence) => _context.WeakPointOccurrences.Remove(occurrence);

        public Task<WeakPoint?> GetByUserAndCatalogAsync(Guid userId, Guid catalogId, CancellationToken cancellationToken = default)
            => _context.WeakPoints.FirstOrDefaultAsync(x => x.UserId == userId && x.CatalogId == catalogId, cancellationToken);

        public Task<List<WeakPoint>> ListByUserAsync(Guid userId, WeakPointStatus? status, CancellationToken cancellationToken = default)
            => _context.WeakPoints
                .Where(x => x.UserId == userId && (status == null || x.Status == status))
                .Include(x => x.Catalog)
                .OrderByDescending(x => x.LastSeenAt)
                .ToListAsync(cancellationToken);

        /// <summary>Default cap on how many weak points the grading prompt injects (design decision: keep the block a fixed size regardless of history depth).</summary>
        public const int GradingPromptLimit = 6;

        public Task<List<WeakPoint>> ListActiveWithCatalogByUserAsync(
            Guid userId, int? limit = null, CancellationToken cancellationToken = default)
        {
            var query = _context.WeakPoints
                .Where(x => x.UserId == userId && x.Status == WeakPointStatus.active)
                .Include(x => x.Catalog)
                // Priority is declared { high, medium, low } — high is ordinal 0, so ascending
                // order puts the most urgent first (see AGENTS.md's note on this enum landmine).
                .OrderBy(x => x.Priority)
                .ThenByDescending(x => x.LastSeenAt)
                .AsQueryable();

            if (limit is { } n)
            {
                query = query.Take(n);
            }

            return query.ToListAsync(cancellationToken);
        }

        public Task<bool> OccurrenceExistsAsync(Guid weakPointId, Guid submissionId, CancellationToken cancellationToken = default)
            => _context.WeakPointOccurrences
                .AnyAsync(x => x.WeakPointId == weakPointId && x.SubmissionId == submissionId, cancellationToken);

        public Task<WeakPointCatalog?> GetCatalogByExamAndCodeAsync(Guid examTypeId, string code, CancellationToken cancellationToken = default)
            => _context.WeakPointCatalog
                .FirstOrDefaultAsync(x => x.ExamTypeId == examTypeId && x.Code == code, cancellationToken);

        public async Task AddAsync(WeakPoint weakPoint, CancellationToken cancellationToken = default)
            => await _context.WeakPoints.AddAsync(weakPoint, cancellationToken);

        public async Task AddCatalogAsync(WeakPointCatalog catalog, CancellationToken cancellationToken = default)
            => await _context.WeakPointCatalog.AddAsync(catalog, cancellationToken);

        public async Task AddOccurrenceAsync(WeakPointOccurrence occurrence, CancellationToken cancellationToken = default)
            => await _context.WeakPointOccurrences.AddAsync(occurrence, cancellationToken);
    }
}
