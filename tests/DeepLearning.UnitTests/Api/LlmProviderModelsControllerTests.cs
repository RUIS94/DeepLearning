using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.LlmProviders.Commands.AddLlmProviderModel;
using DeepLearning.Application.Features.LlmProviders.Commands.SelectLlmProviderModel;
using DeepLearning.Application.Features.LlmProviders.Queries.ListLlmProviderModels;
using DeepLearning.Application.Features.LlmProviders.Queries.ListLlmProviders;
using DeepLearning.Domain.Entities;
using DeepLearning.Infrastructure.Persistence;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DeepLearning.UnitTests.Api
{
    [Collection(ApiCollection.Name)]
    public class LlmProviderModelsControllerTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public LlmProviderModelsControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<string> SeedProviderAsync()
        {
            var providerKey = $"test-provider-{Guid.NewGuid():N}";
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await context.LlmProviderSettings.AddAsync(
                new LlmProviderSettings { Id = Guid.NewGuid(), ProviderKey = providerKey, IsActive = false });
            await context.SaveChangesAsync();

            return providerKey;
        }

        private async Task<string> AddModelAsync(HttpClient client, string providerKey, string model, string? label = null)
        {
            var response = await client.PostAsJsonAsync(
                $"{ApiRoutes.LlmProviderSettings.Base}/{providerKey}/models",
                new { Model = model, Label = label });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return model;
        }

        [Fact]
        public async Task ListModels_returns_404_for_an_unknown_provider()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync($"{ApiRoutes.LlmProviderSettings.Base}/does-not-exist-{Guid.NewGuid():N}/models");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task AddModel_returns_404_for_an_unknown_provider()
        {
            var client = _factory.CreateClient();

            var response = await client.PostAsJsonAsync(
                $"{ApiRoutes.LlmProviderSettings.Base}/does-not-exist-{Guid.NewGuid():N}/models",
                new { Model = "some-model", Label = (string?)null });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task AddModel_then_list_returns_the_added_model_as_not_current()
        {
            var providerKey = await SeedProviderAsync();
            var client = _factory.CreateClient();

            var addResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.LlmProviderSettings.Base}/{providerKey}/models",
                new { Model = "model-b", Label = "Model B" });
            Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);

            var added = await addResponse.Content.ReadFromJsonAsync<AddLlmProviderModelResult>();
            Assert.Equal("model-b", added!.Model);
            Assert.Equal("Model B", added.Label);
            Assert.False(added.IsCurrent);

            var listResponse = await client.GetAsync($"{ApiRoutes.LlmProviderSettings.Base}/{providerKey}/models");
            var models = await listResponse.Content.ReadFromJsonAsync<List<LlmProviderModelResultItem>>();

            Assert.False(models!.Single(x => x.Model == "model-b").IsCurrent);

            // Adding a model to the catalog must not make it (or anything else) the provider's
            // current model — the provider's CurrentModel stays unset until Select is called.
            var settingsResponse = await client.GetAsync(ApiRoutes.LlmProviderSettings.Base);
            var settings = await settingsResponse.Content.ReadFromJsonAsync<List<LlmProviderResultItem>>();
            Assert.Null(settings!.Single(x => x.ProviderKey == providerKey).CurrentModel);
        }

        [Fact]
        public async Task AddModel_returns_409_for_a_model_already_cataloged_for_that_provider()
        {
            var providerKey = await SeedProviderAsync();
            var client = _factory.CreateClient();

            var request = new { Model = "duplicate-model", Label = (string?)null };
            var first = await client.PostAsJsonAsync($"{ApiRoutes.LlmProviderSettings.Base}/{providerKey}/models", request);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);

            var second = await client.PostAsJsonAsync($"{ApiRoutes.LlmProviderSettings.Base}/{providerKey}/models", request);
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        }

        [Fact]
        public async Task Select_returns_404_for_a_model_not_in_the_catalog()
        {
            var providerKey = await SeedProviderAsync();
            var client = _factory.CreateClient();

            var response = await client.PostAsync(
                $"{ApiRoutes.LlmProviderSettings.Base}/{providerKey}/models/not-cataloged/select", content: null);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Select_makes_the_model_current_and_is_reflected_in_the_provider_list()
        {
            var providerKey = await SeedProviderAsync();
            var client = _factory.CreateClient();
            await AddModelAsync(client, providerKey, "model-a");
            await AddModelAsync(client, providerKey, "model-b");

            var selectResponse = await client.PostAsync(
                $"{ApiRoutes.LlmProviderSettings.Base}/{providerKey}/models/model-b/select", content: null);
            Assert.Equal(HttpStatusCode.OK, selectResponse.StatusCode);

            var selected = await selectResponse.Content.ReadFromJsonAsync<SelectLlmProviderModelResult>();
            Assert.True(selected!.IsCurrent);

            var listResponse = await client.GetAsync($"{ApiRoutes.LlmProviderSettings.Base}/{providerKey}/models");
            var models = await listResponse.Content.ReadFromJsonAsync<List<LlmProviderModelResultItem>>();
            Assert.True(models!.Single(x => x.Model == "model-b").IsCurrent);
            Assert.False(models.Single(x => x.Model == "model-a").IsCurrent);

            var settingsResponse = await client.GetAsync(ApiRoutes.LlmProviderSettings.Base);
            var settings = await settingsResponse.Content.ReadFromJsonAsync<List<LlmProviderResultItem>>();
            Assert.Equal("model-b", settings!.Single(x => x.ProviderKey == providerKey).CurrentModel);
        }

        [Fact]
        public async Task Selecting_a_model_for_one_provider_does_not_affect_another_providers_current_model()
        {
            var providerA = await SeedProviderAsync();
            var providerB = await SeedProviderAsync();
            var client = _factory.CreateClient();
            await AddModelAsync(client, providerA, "shared-model-name");
            await AddModelAsync(client, providerB, "shared-model-name");

            var selectA = await client.PostAsync(
                $"{ApiRoutes.LlmProviderSettings.Base}/{providerA}/models/shared-model-name/select", content: null);
            Assert.Equal(HttpStatusCode.OK, selectA.StatusCode);

            var listB = await client.GetAsync($"{ApiRoutes.LlmProviderSettings.Base}/{providerB}/models");
            var modelsB = await listB.Content.ReadFromJsonAsync<List<LlmProviderModelResultItem>>();
            Assert.False(modelsB!.Single(x => x.Model == "shared-model-name").IsCurrent);
        }
    }
}
