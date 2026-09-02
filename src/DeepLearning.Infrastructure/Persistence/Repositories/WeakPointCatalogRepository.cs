using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
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

        public Task<List<WeakPointCatalog>> ListByExamTypeAsync(Guid examTypeId, CancellationToken cancellationToken = default)
            => _context.WeakPointCatalog
                .Where(x => x.ExamTypeId == examTypeId && x.IsActive)
                .ToListAsync(cancellationToken);
    }
}
