using DeepLearning.Domain.Entities;
using DeepLearning.Infrastructure.Persistence.Repositories;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.UnitTests.Integration
{
    [Collection(PostgresCollection.Name)]
    public class LlmProviderSettingsRepositoryTests
    {
        private readonly PostgresContainerFixture _fixture;

        public LlmProviderSettingsRepositoryTests(PostgresContainerFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Get_active_returns_the_one_row_marked_active()
        {
            await using var context = _fixture.CreateContext();

            // The partial unique index allows only one is_active=true row in the whole
            // table, and this class shares one Postgres container across its tests (see
            // PostgresContainerFixture) — clear any row a sibling test left active so this
            // test's own insert below doesn't collide with it.
            await foreach (var stale in context.LlmProviderSettings.Where(x => x.IsActive).AsAsyncEnumerable())
            {
                stale.IsActive = false;
            }
            await context.SaveChangesAsync();

            await context.LlmProviderSettings.AddRangeAsync(
                new LlmProviderSettings { Id = Guid.NewGuid(), ProviderKey = $"test-a-{Guid.NewGuid():N}", IsActive = false, Model = "m1" },
                new LlmProviderSettings { Id = Guid.NewGuid(), ProviderKey = $"test-b-{Guid.NewGuid():N}", IsActive = true, Model = "m2" });
            await context.SaveChangesAsync();

            await using var readContext = _fixture.CreateContext();
            var repository = new LlmProviderSettingsRepository(readContext);
            var active = await repository.GetActiveAsync();

            Assert.NotNull(active);
            Assert.Equal("m2", active!.Model);
        }

        [Fact]
        public async Task The_database_rejects_a_second_active_row()
        {
            var suffix = Guid.NewGuid().ToString("N");

            await using var context = _fixture.CreateContext();
            await context.LlmProviderSettings.AddAsync(
                new LlmProviderSettings { Id = Guid.NewGuid(), ProviderKey = $"test-first-{suffix}", IsActive = true, Model = "m1" });
            await context.SaveChangesAsync();

            await using var secondContext = _fixture.CreateContext();
            await secondContext.LlmProviderSettings.AddAsync(
                new LlmProviderSettings { Id = Guid.NewGuid(), ProviderKey = $"test-second-{suffix}", IsActive = true, Model = "m2" });

            // The partial unique index on IsActive (WHERE is_active = true) is what makes
            // "at most one active provider" a database guarantee, not just app-level discipline.
            await Assert.ThrowsAnyAsync<Exception>(() => secondContext.SaveChangesAsync());
        }
    }
}
