using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class WeakPointCategoryRepository : IWeakPointCategoryRepository
    {
        private readonly AppDbContext _context;

        public WeakPointCategoryRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<WeakPointCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.WeakPointCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public Task<WeakPointCategory?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
            => _context.WeakPointCategories.FirstOrDefaultAsync(x => x.Code == code, cancellationToken);

        public Task<List<WeakPointCategory>> ListAllAsync(CancellationToken cancellationToken = default)
            => _context.WeakPointCategories.OrderBy(x => x.DisplayOrder).ToListAsync(cancellationToken);
    }
}
