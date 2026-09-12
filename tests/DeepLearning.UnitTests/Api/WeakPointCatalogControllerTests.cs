using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.WeakPointCatalogAdmin.Commands.UpdateWeakPointCatalogEntry;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Persistence;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DeepLearning.UnitTests.Api
{
    [Collection(ApiCollection.Name)]
    public class WeakPointCatalogControllerTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public WeakPointCatalogControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<Guid> SeedCatalogEntryAsync(string name, string description)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var category = new WeakPointCategory
            {
                Id = Guid.NewGuid(),
                Code = $"test_cat_{Guid.NewGuid():N}"[..20],
                Name = "Test Category",
                DisplayOrder = 0,
            };
            var entry = new WeakPointCatalog
            {
                Id = Guid.NewGuid(),
                CategoryId = category.Id,
                Code = $"test_entry_{Guid.NewGuid():N}"[..20],
                Name = name,
                Description = description,
                Status = WeakPointCatalogStatus.active,
                Origin = "manual",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await context.WeakPointCategories.AddAsync(category);
            await context.WeakPointCatalog.AddAsync(entry);
            await context.SaveChangesAsync();

            return entry.Id;
        }

        /// <summary>
        /// Locks in the fix from 代码复用扫描_07_优化计划.md §3.6: Name/Description used to use an
        /// IsNullOrWhiteSpace guard (silently ignoring a blank submission instead of clearing),
        /// inconsistent with DefaultDimensionKey/DefaultErrorCategory's null=leave/""=clear
        /// convention on the same handler. Now all four fields share the one convention.
        /// </summary>
        [Fact]
        public async Task Update_with_an_empty_string_clears_name_and_description_while_null_leaves_them_unchanged()
        {
            var id = await SeedCatalogEntryAsync(name: "Original Name", description: "Original description");
            var client = await _factory.CreateAuthenticatedAdminClientAsync();

            var response = await client.PutAsJsonAsync(
                $"{ApiRoutes.WeakPointCatalog.Base}/{id}",
                new { Name = "", Description = (string?)null, DefaultDimensionKey = (string?)null, DefaultErrorCategory = (string?)null, Status = (WeakPointCatalogStatus?)null });

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<UpdateWeakPointCatalogEntryResult>();

            Assert.Equal(string.Empty, result!.Name);

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entry = await context.WeakPointCatalog.FindAsync(id);
            Assert.Equal(string.Empty, entry!.Name);
            Assert.Equal("Original description", entry.Description);
        }

        [Fact]
        public async Task Update_with_a_non_null_name_sets_it()
        {
            var id = await SeedCatalogEntryAsync(name: "Original Name", description: "Original description");
            var client = await _factory.CreateAuthenticatedAdminClientAsync();

            var response = await client.PutAsJsonAsync(
                $"{ApiRoutes.WeakPointCatalog.Base}/{id}",
                new { Name = "Renamed", Description = (string?)null, DefaultDimensionKey = (string?)null, DefaultErrorCategory = (string?)null, Status = (WeakPointCatalogStatus?)null });

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<UpdateWeakPointCatalogEntryResult>();

            Assert.Equal("Renamed", result!.Name);
        }
    }
}
