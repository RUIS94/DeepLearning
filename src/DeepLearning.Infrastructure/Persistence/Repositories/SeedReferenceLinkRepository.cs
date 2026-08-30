using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class SeedReferenceLinkRepository : ISeedReferenceLinkRepository
    {
        private readonly AppDbContext _context;

        public SeedReferenceLinkRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<List<SeedReferenceLink>> ListByGeneratedQuestionIdAsync(Guid generatedQuestionId, CancellationToken cancellationToken = default)
            => _context.SeedReferenceLinks
                .Where(x => x.GeneratedQuestionId == generatedQuestionId)
                .Include(x => x.SeedQuestion)
                .ToListAsync(cancellationToken);

        public async Task AddRangeAsync(IEnumerable<SeedReferenceLink> links, CancellationToken cancellationToken = default)
            => await _context.SeedReferenceLinks.AddRangeAsync(links, cancellationToken);
    }
}
