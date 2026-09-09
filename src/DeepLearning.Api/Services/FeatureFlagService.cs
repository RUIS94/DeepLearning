using DeepLearning.Application.Common;
using DeepLearning.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace DeepLearning.Api.Services
{
    /// <inheritdoc cref="IFeatureFlagService"/>
    public class FeatureFlagService : IFeatureFlagService
    {
        /// <summary>
        /// Short enough that flipping a flag in the DB takes effect within seconds without a
        /// deploy, long enough that a burst of requests to a gated endpoint isn't a burst of
        /// identical SELECTs. Overridable so tests can disable caching (<see cref="TimeSpan.Zero"/>).
        /// </summary>
        public static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromSeconds(15);

        private readonly IFeatureFlagRepository _repository;
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _ttl;

        public FeatureFlagService(IFeatureFlagRepository repository, IMemoryCache cache, TimeSpan? cacheTtl = null)
        {
            _repository = repository;
            _cache = cache;
            _ttl = cacheTtl ?? DefaultCacheTtl;
        }

        public async Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"feature-flag:{key}";
            if (_ttl > TimeSpan.Zero && _cache.TryGetValue<bool>(cacheKey, out var cached))
            {
                return cached;
            }

            var row = await _repository.GetByKeyAsync(key, cancellationToken);
            var enabled = row?.Enabled ?? FeatureFlags.DefaultFor(key);

            if (_ttl > TimeSpan.Zero)
            {
                _cache.Set(cacheKey, enabled, _ttl);
            }

            return enabled;
        }

        public void Invalidate(string key) => _cache.Remove($"feature-flag:{key}");
    }
}
