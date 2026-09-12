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

        /// <summary>No test here exercises per-user overrides (see UserFeatureOverrideTests-level coverage instead) — always "no override row".</summary>
        private sealed class NoOverridesRepo : IUserFeatureOverrideRepository
        {
            public Task<UserFeatureOverride?> GetAsync(Guid userId, string featureKey, CancellationToken cancellationToken = default)
                => Task.FromResult<UserFeatureOverride?>(null);

            public Task<List<UserFeatureOverride>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
                => Task.FromResult(new List<UserFeatureOverride>());

            public Task SetAsync(Guid userId, string featureKey, bool enabled, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public Task ClearAsync(Guid userId, string featureKey, CancellationToken cancellationToken = default)
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
            var service = new FeatureFlagService(new FakeRepo(null), new NoOverridesRepo(), NewCache(), TimeSpan.Zero);

            Assert.True(await service.IsEnabledAsync(FeatureFlags.QuestionBankEnabled));
            Assert.True(await service.IsEnabledAsync(FeatureFlags.ReviewLibraryEnabled));
            Assert.False(await service.IsEnabledAsync("some_unknown_flag"));
        }

        [Fact]
        public async Task Returns_the_stored_value_when_a_row_exists()
        {
            var service = new FeatureFlagService(
                new FakeRepo(Row(FeatureFlags.QuestionBankEnabled, enabled: false)), new NoOverridesRepo(), NewCache(), TimeSpan.Zero);

            Assert.False(await service.IsEnabledAsync(FeatureFlags.QuestionBankEnabled));
        }

        [Fact]
        public async Task Caches_within_the_ttl_so_a_burst_is_one_db_read()
        {
            var repo = new FakeRepo(Row(FeatureFlags.ReviewLibraryEnabled, enabled: true));
            var service = new FeatureFlagService(repo, new NoOverridesRepo(), NewCache(), TimeSpan.FromMinutes(5));

            await service.IsEnabledAsync(FeatureFlags.ReviewLibraryEnabled);
            await service.IsEnabledAsync(FeatureFlags.ReviewLibraryEnabled);
            await service.IsEnabledAsync(FeatureFlags.ReviewLibraryEnabled);

            Assert.Equal(1, repo.GetCalls);
        }

        [Fact]
        public async Task A_zero_ttl_reads_fresh_every_time()
        {
            var repo = new FakeRepo(Row(FeatureFlags.ReviewLibraryEnabled, enabled: true));
            var service = new FeatureFlagService(repo, new NoOverridesRepo(), NewCache(), TimeSpan.Zero);

            await service.IsEnabledAsync(FeatureFlags.ReviewLibraryEnabled);
            await service.IsEnabledAsync(FeatureFlags.ReviewLibraryEnabled);

            Assert.Equal(2, repo.GetCalls);
        }

        private sealed class SingleOverrideRepo : IUserFeatureOverrideRepository
        {
            private readonly UserFeatureOverride _row;

            public SingleOverrideRepo(UserFeatureOverride row) => _row = row;

            public Task<UserFeatureOverride?> GetAsync(Guid userId, string featureKey, CancellationToken cancellationToken = default)
                => Task.FromResult(userId == _row.UserId && featureKey == _row.FeatureKey ? _row : null);

            public Task<List<UserFeatureOverride>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
                => Task.FromResult(userId == _row.UserId ? new List<UserFeatureOverride> { _row } : new List<UserFeatureOverride>());

            public Task SetAsync(Guid userId, string featureKey, bool enabled, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();

            public Task ClearAsync(Guid userId, string featureKey, CancellationToken cancellationToken = default)
                => throw new NotImplementedException();
        }

        [Fact]
        public async Task A_user_override_wins_over_the_global_flag_but_only_for_that_user()
        {
            var userId = Guid.NewGuid();
            var overrideRepo = new SingleOverrideRepo(new UserFeatureOverride
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                FeatureKey = FeatureFlags.QuestionBankEnabled,
                Enabled = false,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            var service = new FeatureFlagService(
                new FakeRepo(Row(FeatureFlags.QuestionBankEnabled, enabled: true)), overrideRepo, NewCache(), TimeSpan.Zero);

            Assert.False(await service.IsEnabledAsync(FeatureFlags.QuestionBankEnabled, userId));
            Assert.True(await service.IsEnabledAsync(FeatureFlags.QuestionBankEnabled, Guid.NewGuid()));
            Assert.True(await service.IsEnabledAsync(FeatureFlags.QuestionBankEnabled));
        }
    }
}
