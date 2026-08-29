using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class ErrorTaxonomyRepository : IErrorTaxonomyRepository
    {
        private readonly AppDbContext _context;

        public ErrorTaxonomyRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<ErrorTaxonomy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.ErrorTaxonomies.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public Task<List<ErrorTaxonomy>> ListByExamTypeAsync(Guid examTypeId, CancellationToken cancellationToken = default)
            => _context.ErrorTaxonomies
                .Where(x => x.ExamTypeId == examTypeId)
                .OrderBy(x => x.CategoryKey)
                .ToListAsync(cancellationToken);

        public Task<bool> ExistsAsync(Guid examTypeId, string categoryKey, CancellationToken cancellationToken = default)
            => _context.ErrorTaxonomies.AnyAsync(
                x => x.ExamTypeId == examTypeId && x.CategoryKey == categoryKey,
                cancellationToken);

        public async Task AddAsync(ErrorTaxonomy taxonomy, CancellationToken cancellationToken = default)
            => await _context.ErrorTaxonomies.AddAsync(taxonomy, cancellationToken);
    }
}
