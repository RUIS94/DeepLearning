using DeepLearning.Api.Services;
using DeepLearning.Application.Common;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace DeepLearning.UnitTests.Api
{
    public class FeatureFlagServiceTests
    {
        private sealed class FakeRepo : IFeatureFlagRepository
        {
            private readonly FeatureFlag? _row;
            public int GetCalls { get; private set; }

            public FakeRepo(FeatureFlag? row) => _row = row;

            public Task<FeatureFlag?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
            {
                GetCalls++;
                return Task.FromResult(_row is not null && _row.Key == key ? _row : null);
            }

            public Task<List<FeatureFlag>> ListAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(_row is null ? new List<FeatureFlag>() : [_row]);

            public Task SetEnabledAsync(string key, bool enabled, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();
        }

        private static IMemoryCache NewCache() => new MemoryCache(new MemoryCacheOptions());

        private static FeatureFlag Row(string key, bool enabled) => new()
        {
            Id = Guid.NewGuid(),
            Key = key,
            Enabled = enabled,
            Scope = "global",
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        [Fact]
        public async Task Falls_back_to_the_documented_default_when_no_row_exists()
        {
            var service = new FeatureFlagService(new FakeRepo(null), NewCache(), TimeSpan.Zero);

            Assert.True(await service.IsEnabledAsync(FeatureFlags.QuestionBankEnabled));
            Assert.True(await service.IsEnabledAsync(FeatureFlags.ReviewLibraryEnabled));
            Assert.False(await service.IsEnabledAsync("some_unknown_flag"));
        }

        [Fact]
        public async Task Returns_the_stored_value_when_a_row_exists()
        {
            var service = new FeatureFlagService(
                new FakeRepo(Row(FeatureFlags.QuestionBankEnabled, enabled: false)), NewCache(), TimeSpan.Zero);

            Assert.False(await service.IsEnabledAsync(FeatureFlags.QuestionBankEnabled));
        }

        [Fact]
        public async Task Caches_within_the_ttl_so_a_burst_is_one_db_read()
        {
            var repo = new FakeRepo(Row(FeatureFlags.ReviewLibraryEnabled, enabled: true));
            var service = new FeatureFlagService(repo, NewCache(), TimeSpan.FromMinutes(5));

            await service.IsEnabledAsync(FeatureFlags.ReviewLibraryEnabled);
            await service.IsEnabledAsync(FeatureFlags.ReviewLibraryEnabled);
            await service.IsEnabledAsync(FeatureFlags.ReviewLibraryEnabled);

            Assert.Equal(1, repo.GetCalls);
        }

        [Fact]
        public async Task A_zero_ttl_reads_fresh_every_time()
        {
            var repo = new FakeRepo(Row(FeatureFlags.ReviewLibraryEnabled, enabled: true));
            var service = new FeatureFlagService(repo, NewCache(), TimeSpan.Zero);

            await service.IsEnabledAsync(FeatureFlags.ReviewLibraryEnabled);
            await service.IsEnabledAsync(FeatureFlags.ReviewLibraryEnabled);

            Assert.Equal(2, repo.GetCalls);
        }
    }
}
