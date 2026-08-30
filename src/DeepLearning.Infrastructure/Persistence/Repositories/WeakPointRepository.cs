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

        public Task<WeakPoint?> GetByUserAndCategoryAsync(Guid userId, string category, CancellationToken cancellationToken = default)
            => _context.WeakPoints.FirstOrDefaultAsync(x => x.UserId == userId && x.Category == category, cancellationToken);

        public Task<List<WeakPoint>> ListByUserAsync(Guid userId, WeakPointStatus? status, CancellationToken cancellationToken = default)
            => _context.WeakPoints
                .Where(x => x.UserId == userId && (status == null || x.Status == status))
                .OrderByDescending(x => x.LastSeenAt)
                .ToListAsync(cancellationToken);

        public async Task AddAsync(WeakPoint weakPoint, CancellationToken cancellationToken = default)
            => await _context.WeakPoints.AddAsync(weakPoint, cancellationToken);

        public async Task AddOccurrenceAsync(WeakPointOccurrence occurrence, CancellationToken cancellationToken = default)
            => await _context.WeakPointOccurrences.AddAsync(occurrence, cancellationToken);
    }
}
