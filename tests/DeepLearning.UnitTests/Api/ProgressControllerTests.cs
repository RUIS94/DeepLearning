using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.Progress.Queries.GetProgressSnapshots;
using DeepLearning.Domain.Entities;
using DeepLearning.Infrastructure.Persistence;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DeepLearning.UnitTests.Api
{
    /// <summary>
    /// Design doc §11.2 Step 9's own test list: "API测试(进度查询接口)" — GET /api/v1/progress.
    /// Snapshot rows are seeded directly via DbContext (same convention as every other
    /// read-only-endpoint test in this codebase, e.g. DeepLearningContentControllerTests) rather
    /// than going through GenerateProgressTrendSnapshotCommandHandler, since that handler's own
    /// AI-orchestration behavior is already covered by Integration/GenerateProgressTrendSnapshotCommandHandlerTests.cs.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class ProgressControllerTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public ProgressControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task SeedSnapshotAsync(Guid userId, DateOnly periodStart, DateOnly periodEnd, string? difficultyTier, decimal? passRate)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.ProgressSnapshots.AddAsync(new ProgressSnapshot
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                DifficultyTier = difficultyTier,
                PassRate = passRate,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task List_returns_every_snapshot_for_the_user_ordered_oldest_first()
        {
            var client = _factory.CreateClient();
            var userId = await _factory.SeedUserAsync();

            await SeedSnapshotAsync(userId, new DateOnly(2026, 8, 24), new DateOnly(2026, 8, 30), "medium", 50.00m);
            await SeedSnapshotAsync(userId, new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 23), "medium", 100.00m);

            var response = await client.GetAsync($"{ApiRoutes.Progress.Base}?userId={userId}");
            response.EnsureSuccessStatusCode();
            var items = await response.Content.ReadFromJsonAsync<List<ProgressSnapshotResultItem>>();

            Assert.Equal(2, items!.Count);
            Assert.Equal(new DateOnly(2026, 8, 17), items[0].PeriodStart);
            Assert.Equal(100.00m, items[0].PassRate);
            Assert.Equal(new DateOnly(2026, 8, 24), items[1].PeriodStart);
            Assert.Equal(50.00m, items[1].PassRate);
        }

        [Fact]
        public async Task List_filters_by_difficulty_tier_when_supplied()
        {
            var client = _factory.CreateClient();
            var userId = await _factory.SeedUserAsync();

            await SeedSnapshotAsync(userId, new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 23), "easy", 100.00m);
            await SeedSnapshotAsync(userId, new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 23), "hard", 20.00m);

            var response = await client.GetAsync($"{ApiRoutes.Progress.Base}?userId={userId}&difficultyTier=hard");
            response.EnsureSuccessStatusCode();
            var items = await response.Content.ReadFromJsonAsync<List<ProgressSnapshotResultItem>>();

            var item = Assert.Single(items!);
            Assert.Equal("hard", item.DifficultyTier);
        }

        [Fact]
        public async Task List_returns_an_empty_list_for_a_user_with_no_snapshots()
        {
            var client = _factory.CreateClient();
            var userId = await _factory.SeedUserAsync();

            var response = await client.GetAsync($"{ApiRoutes.Progress.Base}?userId={userId}");
            response.EnsureSuccessStatusCode();
            var items = await response.Content.ReadFromJsonAsync<List<ProgressSnapshotResultItem>>();

            Assert.Empty(items!);
        }
    }
}
