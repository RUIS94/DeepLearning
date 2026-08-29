using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.LlmProviders.Commands.ActivateLlmProvider;
using DeepLearning.Application.Features.LlmProviders.Commands.UpdateLlmProviderSettings;
using DeepLearning.Application.Features.LlmProviders.Queries.ListLlmProviders;
using DeepLearning.Domain.Entities;
using DeepLearning.Infrastructure.Persistence;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DeepLearning.UnitTests.Api
{
    [Collection(ApiCollection.Name)]
    public class LlmProviderSettingsControllerTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public LlmProviderSettingsControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task List_returns_ok()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync(ApiRoutes.LlmProviderSettings.Base);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Update_returns_404_for_an_unknown_provider()
        {
            var client = _factory.CreateClient();

            var response = await client.PatchAsync(
                $"{ApiRoutes.LlmProviderSettings.Base}/does-not-exist-{Guid.NewGuid():N}",
                JsonContent.Create(new { ThinkingEnabled = (bool?)null, Effort = (string?)"high", ExtraSettingsJson = (string?)null }));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Activate_returns_404_for_an_unknown_provider()
        {
            var client = _factory.CreateClient();

            var response = await client.PostAsync(
                $"{ApiRoutes.LlmProviderSettings.Base}/does-not-exist-{Guid.NewGuid():N}/activate", content: null);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        private async Task<(string First, string Second)> SeedTwoProvidersAsync(bool firstIsActive)
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // This collection shares one Postgres container/DB across every test in the class,
            // and rows from earlier tests are never cleaned up — so a leftover is_active=true
            // row from another test would collide with ux_llm_provider_settings_single_active
            // as soon as this helper inserts its own active row. Clear any pre-existing active
            // rows first so each test's seed data is the only thing determining what's active.
            await context.LlmProviderSettings
                .Where(x => x.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));

            var first = $"test-a-{suffix}";
            var second = $"test-b-{suffix}";
            await context.LlmProviderSettings.AddRangeAsync(
                new LlmProviderSettings { Id = Guid.NewGuid(), ProviderKey = first, IsActive = firstIsActive },
                new LlmProviderSettings { Id = Guid.NewGuid(), ProviderKey = second, IsActive = !firstIsActive });
            await context.SaveChangesAsync();

            return (first, second);
        }

        [Fact]
        public async Task Update_changes_only_the_fields_that_were_provided()
        {
            var (providerKey, _) = await SeedTwoProvidersAsync(firstIsActive: false);
            var client = _factory.CreateClient();

            var response = await client.PatchAsJsonAsync(
                $"{ApiRoutes.LlmProviderSettings.Base}/{providerKey}",
                new { ThinkingEnabled = (bool?)null, Effort = "high", ExtraSettingsJson = (string?)null });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var updated = await response.Content.ReadFromJsonAsync<UpdateLlmProviderSettingsResult>();
            Assert.Equal("high", updated!.Effort);
            Assert.True(updated.ThinkingEnabled); // untouched, entity default
        }

        [Fact]
        public async Task Activate_deactivates_the_previously_active_provider()
        {
            var (first, second) = await SeedTwoProvidersAsync(firstIsActive: false);
            var client = _factory.CreateClient();

            var activateResponse = await client.PostAsync($"{ApiRoutes.LlmProviderSettings.Base}/{first}/activate", content: null);
            Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);

            var listResponse = await client.GetAsync(ApiRoutes.LlmProviderSettings.Base);
            var all = await listResponse.Content.ReadFromJsonAsync<List<LlmProviderResultItem>>();

            Assert.True(all!.Single(x => x.ProviderKey == first).IsActive);
            Assert.False(all.Single(x => x.ProviderKey == second).IsActive);
        }
    }
}
