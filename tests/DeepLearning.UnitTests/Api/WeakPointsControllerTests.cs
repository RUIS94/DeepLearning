using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.WeakPoints.Commands.ReclassifyWeakPoint;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Persistence;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DeepLearning.UnitTests.Api
{
    /// <summary>
    /// ReclassifyWeakPointCommandHandler never checked that the weak point belonged to the caller
    /// (ref/管理员与用户权限隔离_策划书.md "二轮复查补漏") — any authenticated user could reclassify, or
    /// even merge away, another user's weak point by guessing its id. These lock in the fix.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class WeakPointsControllerTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public WeakPointsControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<Guid> SeedCatalogEntryAsync(AppDbContext context)
        {
            var category = new WeakPointCategory
            {
                Id = Guid.NewGuid(),
                Code = $"cat_{Guid.NewGuid():N}"[..20],
                Name = "Test Category",
                DisplayOrder = 0,
            };
            var entry = new WeakPointCatalog
            {
                Id = Guid.NewGuid(),
                CategoryId = category.Id,
                Code = $"entry_{Guid.NewGuid():N}"[..20],
                Name = "Target kind",
                Description = "Target kind",
                Status = WeakPointCatalogStatus.active,
                Origin = "manual",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await context.WeakPointCategories.AddAsync(category);
            await context.WeakPointCatalog.AddAsync(entry);
            await context.SaveChangesAsync();
            return entry.Id;
        }

        private async Task<Guid> SeedWeakPointAsync(Guid ownerId)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var weakPoint = new WeakPoint
            {
                Id = Guid.NewGuid(),
                UserId = ownerId,
                Category = "legacy_bucket",
                FirstDetectedAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow,
                Status = WeakPointStatus.active,
                DetectionSource = "rule",
            };
            await context.WeakPoints.AddAsync(weakPoint);
            await context.SaveChangesAsync();
            return weakPoint.Id;
        }

        [Fact]
        public async Task Reclassify_returns_404_when_the_weak_point_belongs_to_a_different_user()
        {
            var owner = await _factory.SeedUserAsync();
            var weakPointId = await SeedWeakPointAsync(owner);

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var catalogId = await SeedCatalogEntryAsync(context);

            var attacker = _factory.CreateAuthenticatedClient();

            var response = await attacker.PostAsJsonAsync(
                $"{ApiRoutes.WeakPoints.Base}/{weakPointId}/reclassify", new { CatalogId = catalogId });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Reclassify_lets_the_owner_move_their_own_weak_point_onto_a_new_catalog_kind()
        {
            var ownerId = await _factory.SeedUserAsync();
            var weakPointId = await SeedWeakPointAsync(ownerId);

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var catalogId = await SeedCatalogEntryAsync(context);

            var ownerClient = _factory.CreateAuthenticatedClient(ownerId);
            var response = await ownerClient.PostAsJsonAsync(
                $"{ApiRoutes.WeakPoints.Base}/{weakPointId}/reclassify", new { CatalogId = catalogId });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<ReclassifyWeakPointResult>();
            Assert.Equal(catalogId, result!.CatalogId);
            Assert.False(result.MergedIntoExisting);
        }
    }
}
