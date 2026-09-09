using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class FeatureFlagRepository : IFeatureFlagRepository
    {
        private readonly AppDbContext _context;

        public FeatureFlagRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<FeatureFlag?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
            => _context.FeatureFlags.AsNoTracking().FirstOrDefaultAsync(x => x.Key == key, cancellationToken);

        public Task<List<FeatureFlag>> ListAsync(CancellationToken cancellationToken = default)
            => _context.FeatureFlags.AsNoTracking().OrderBy(x => x.Key).ToListAsync(cancellationToken);

        public async Task SetEnabledAsync(string key, bool enabled, CancellationToken cancellationToken = default)
        {
            var existing = await _context.FeatureFlags.FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
            if (existing is null)
            {
                await _context.FeatureFlags.AddAsync(new FeatureFlag
                {
                    Id = Guid.NewGuid(),
                    Key = key,
                    Enabled = enabled,
                    Scope = "global",
                    UpdatedAt = DateTimeOffset.UtcNow,
                }, cancellationToken);
                return;
            }

            existing.Enabled = enabled;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
