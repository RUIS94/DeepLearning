using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class WeakPointCatalogRepository : IWeakPointCatalogRepository
    {
        private readonly AppDbContext _context;

        public WeakPointCatalogRepository(AppDbContext context)
        {
            _context = context;
        }

        // proposed + active both participate in rule/AI matching; only deprecated is excluded
        // (a merged-away kind must stop attracting new weak points).
        public Task<List<WeakPointCatalog>> ListByExamTypeAsync(Guid examTypeId, CancellationToken cancellationToken = default)
            => _context.WeakPointCatalog
                .Where(x => x.ExamTypeId == examTypeId && x.Status != WeakPointCatalogStatus.deprecated)
                .ToListAsync(cancellationToken);

        public Task<List<WeakPointCatalog>> ListAllByExamTypeAsync(Guid examTypeId, CancellationToken cancellationToken = default)
            => _context.WeakPointCatalog
                .Where(x => x.ExamTypeId == examTypeId)
                .OrderBy(x => x.Status)
                .ThenBy(x => x.Code)
                .ToListAsync(cancellationToken);

        public Task<WeakPointCatalog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.WeakPointCatalog.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public Task<bool> ExistsAsync(Guid examTypeId, string code, CancellationToken cancellationToken = default)
            => _context.WeakPointCatalog.AnyAsync(x => x.ExamTypeId == examTypeId && x.Code == code, cancellationToken);

        public async Task AddAsync(WeakPointCatalog catalog, CancellationToken cancellationToken = default)
            => await _context.WeakPointCatalog.AddAsync(catalog, cancellationToken);
    }
}
