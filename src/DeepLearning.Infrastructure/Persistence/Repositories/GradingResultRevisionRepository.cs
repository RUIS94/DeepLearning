using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class GradingResultRevisionRepository : IGradingResultRevisionRepository
    {
        private readonly AppDbContext _context;

        public GradingResultRevisionRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(GradingResultRevision revision, CancellationToken cancellationToken = default)
            => await _context.GradingResultRevisions.AddAsync(revision, cancellationToken);
    }
}
