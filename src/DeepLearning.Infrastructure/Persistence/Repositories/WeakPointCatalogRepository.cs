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
        public Task<List<WeakPointCatalog>> ListActiveAsync(CancellationToken cancellationToken = default)
            => _context.WeakPointCatalog
                .Include(x => x.Category)
                .Where(x => x.Status != WeakPointCatalogStatus.deprecated)
                .ToListAsync(cancellationToken);

        public Task<List<WeakPointCatalog>> ListAllAsync(CancellationToken cancellationToken = default)
            => _context.WeakPointCatalog
                .Include(x => x.Category)
                .OrderBy(x => x.Status)
                .ThenBy(x => x.Code)
                .ToListAsync(cancellationToken);

        public Task<WeakPointCatalog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.WeakPointCatalog.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public Task<WeakPointCatalog?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
            => _context.WeakPointCatalog.FirstOrDefaultAsync(x => x.Code == code, cancellationToken);

        public Task<bool> ExistsAsync(string code, CancellationToken cancellationToken = default)
            => _context.WeakPointCatalog.AnyAsync(x => x.Code == code, cancellationToken);

        public async Task AddAsync(WeakPointCatalog catalog, CancellationToken cancellationToken = default)
            => await _context.WeakPointCatalog.AddAsync(catalog, cancellationToken);
    }
}
