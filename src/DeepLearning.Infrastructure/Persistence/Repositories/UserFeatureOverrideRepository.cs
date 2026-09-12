using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class UserFeatureOverrideRepository : IUserFeatureOverrideRepository
    {
        private readonly AppDbContext _context;

        public UserFeatureOverrideRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<UserFeatureOverride?> GetAsync(Guid userId, string featureKey, CancellationToken cancellationToken = default)
            => _context.UserFeatureOverrides.AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.FeatureKey == featureKey, cancellationToken);

        public Task<List<UserFeatureOverride>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
            => _context.UserFeatureOverrides.AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.FeatureKey)
                .ToListAsync(cancellationToken);

        public async Task SetAsync(Guid userId, string featureKey, bool enabled, CancellationToken cancellationToken = default)
        {
            var existing = await _context.UserFeatureOverrides
                .FirstOrDefaultAsync(x => x.UserId == userId && x.FeatureKey == featureKey, cancellationToken);

            if (existing is null)
            {
                await _context.UserFeatureOverrides.AddAsync(new UserFeatureOverride
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    FeatureKey = featureKey,
                    Enabled = enabled,
                    UpdatedAt = DateTimeOffset.UtcNow,
                }, cancellationToken);
                return;
            }

            existing.Enabled = enabled;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        public async Task ClearAsync(Guid userId, string featureKey, CancellationToken cancellationToken = default)
        {
            var existing = await _context.UserFeatureOverrides
                .FirstOrDefaultAsync(x => x.UserId == userId && x.FeatureKey == featureKey, cancellationToken);
            if (existing is not null)
            {
                _context.UserFeatureOverrides.Remove(existing);
            }
        }
    }
}
