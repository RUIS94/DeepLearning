using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.LlmProviders.Commands.ActivateLlmProvider;
using DeepLearning.Application.Features.LlmProviders.Commands.SetAiOperationOverride;
using DeepLearning.Application.Features.LlmProviders.Commands.UpdateLlmProviderSettings;
using DeepLearning.Application.Features.LlmProviders.Queries.ListAiOperationOverrides;
using DeepLearning.Application.Features.LlmProviders.Queries.ListLlmProviders;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
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

        [Fact]
        public async Task SetOperationOverride_returns_404_for_an_unknown_provider()
        {
            var client = _factory.CreateClient();

            var response = await client.PutAsJsonAsync(
                $"{ApiRoutes.LlmProviderSettings.Base}/operation-overrides/{AiOperationType.grading}",
                new { ProviderKey = $"does-not-exist-{Guid.NewGuid():N}" });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task SetOperationOverride_then_ListOperationOverrides_reflects_the_pin_and_Clear_removes_it()
        {
            var (providerKey, _) = await SeedTwoProvidersAsync(firstIsActive: false);
            var client = _factory.CreateClient();

            // weak_point_recheck is exercised least by the rest of the suite, so pinning it here
            // is the least likely operation type to collide with a leftover row from another test
            // sharing this Postgres container/DB.
            const AiOperationType operationType = AiOperationType.weak_point_recheck;

            var setResponse = await client.PutAsJsonAsync(
                $"{ApiRoutes.LlmProviderSettings.Base}/operation-overrides/{operationType}",
                new { ProviderKey = providerKey });
            Assert.Equal(HttpStatusCode.OK, setResponse.StatusCode);
            var set = await setResponse.Content.ReadFromJsonAsync<SetAiOperationOverrideResult>();
            Assert.Equal(providerKey, set!.ProviderKey);

            var listResponse = await client.GetAsync($"{ApiRoutes.LlmProviderSettings.Base}/operation-overrides");
            var all = await listResponse.Content.ReadFromJsonAsync<List<AiOperationOverrideResultItem>>();
            Assert.Equal(providerKey, all!.Single(x => x.OperationType == operationType).ProviderKey);

            var clearResponse = await client.DeleteAsync($"{ApiRoutes.LlmProviderSettings.Base}/operation-overrides/{operationType}");
            Assert.Equal(HttpStatusCode.NoContent, clearResponse.StatusCode);

            var afterClearResponse = await client.GetAsync($"{ApiRoutes.LlmProviderSettings.Base}/operation-overrides");
            var afterClear = await afterClearResponse.Content.ReadFromJsonAsync<List<AiOperationOverrideResultItem>>();
            Assert.Null(afterClear!.Single(x => x.OperationType == operationType).ProviderKey);
        }

        [Fact]
        public async Task SetOperationOverride_returns_404_for_a_model_not_in_that_providers_catalog()
        {
            var (providerKey, _) = await SeedTwoProvidersAsync(firstIsActive: false);
            var client = _factory.CreateClient();

            var response = await client.PutAsJsonAsync(
                $"{ApiRoutes.LlmProviderSettings.Base}/operation-overrides/{AiOperationType.weak_point_classification}",
                new { ProviderKey = providerKey, Model = "no-such-model" });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task SetOperationOverride_pins_a_specific_model_thinking_and_effort_independent_of_the_providers_own_defaults()
        {
            var (providerKey, _) = await SeedTwoProvidersAsync(firstIsActive: false);
            var client = _factory.CreateClient();

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                context.LlmProviderModels.Add(new LlmProviderModel
                {
                    Id = Guid.NewGuid(),
                    ProviderKey = providerKey,
                    Model = "pinned-model",
                    IsCurrent = false,
                });
                await context.SaveChangesAsync();
            }

            // weak_point_detection_criteria is exercised least by the rest of the suite — see the
            // same reasoning on weak_point_recheck above.
            const AiOperationType operationType = AiOperationType.weak_point_detection_criteria;

            var setResponse = await client.PutAsJsonAsync(
                $"{ApiRoutes.LlmProviderSettings.Base}/operation-overrides/{operationType}",
                new { ProviderKey = providerKey, Model = "pinned-model", ThinkingEnabled = false, Effort = "xhigh" });
            Assert.Equal(HttpStatusCode.OK, setResponse.StatusCode);
            var set = await setResponse.Content.ReadFromJsonAsync<SetAiOperationOverrideResult>();
            Assert.Equal("pinned-model", set!.Model);
            Assert.False(set.ThinkingEnabled);
            Assert.Equal("xhigh", set.Effort);

            var listResponse = await client.GetAsync($"{ApiRoutes.LlmProviderSettings.Base}/operation-overrides");
            var all = await listResponse.Content.ReadFromJsonAsync<List<AiOperationOverrideResultItem>>();
            var row = all!.Single(x => x.OperationType == operationType);
            Assert.Equal("pinned-model", row.Model);
            Assert.False(row.ThinkingEnabled);
            Assert.Equal("xhigh", row.Effort);
        }
    }
}
