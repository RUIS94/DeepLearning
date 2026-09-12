using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Common;
using DeepLearning.Application.Features.FeatureFlags.Queries.ListFeatureFlags;
using DeepLearning.Domain.Entities;
using DeepLearning.Infrastructure.Persistence;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DeepLearning.UnitTests.Api
{
    /// <summary>
    /// GET/PUT /api/v1/feature-flags backs the settings screen's "功能开关" toggles. The list
    /// merges rows with the code defaults (so a key with no row still shows), and a PUT is an
    /// upsert that the gate honours immediately (the flag service is registered with a zero
    /// cache TTL in the test host).
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class FeatureFlagsControllerTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public FeatureFlagsControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task RemoveRowAsync(string key)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await context.FeatureFlags.FirstOrDefaultAsync(x => x.Key == key);
            if (row is not null)
            {
                context.FeatureFlags.Remove(row);
                await context.SaveChangesAsync();
            }
        }

        [Fact]
        public async Task List_returns_every_known_flag_even_when_it_has_no_row()
        {
            await RemoveRowAsync(FeatureFlags.QuestionBankEnabled);
            await RemoveRowAsync(FeatureFlags.ReviewLibraryEnabled);
            var client = await _factory.CreateAuthenticatedAdminClientAsync();

            var flags = await client.GetFromJsonAsync<List<FeatureFlagResultItem>>(ApiRoutes.FeatureFlags.Base);

            Assert.NotNull(flags);
            var review = Assert.Single(flags!, f => f.Key == FeatureFlags.ReviewLibraryEnabled);
            Assert.True(review.Enabled);  // code default
            Assert.False(review.HasRow);
            Assert.Contains(flags!, f => f.Key == FeatureFlags.QuestionBankEnabled);
        }

        [Fact]
        public async Task Setting_a_flag_to_false_makes_its_gated_endpoint_404_then_true_restores_it()
        {
            var client = await _factory.CreateAuthenticatedAdminClientAsync();
            try
            {
                var off = await client.PutAsJsonAsync(
                    $"{ApiRoutes.FeatureFlags.Base}/{FeatureFlags.ReviewLibraryEnabled}", new { Enabled = false });
                Assert.Equal(HttpStatusCode.OK, off.StatusCode);

                var gated = await client.GetAsync($"{ApiRoutes.ReviewLibrary.Base}/patterns?userId={Guid.NewGuid()}");
                Assert.Equal(HttpStatusCode.NotFound, gated.StatusCode);

                var listed = await client.GetFromJsonAsync<List<FeatureFlagResultItem>>(ApiRoutes.FeatureFlags.Base);
                Assert.False(Assert.Single(listed!, f => f.Key == FeatureFlags.ReviewLibraryEnabled).Enabled);

                var on = await client.PutAsJsonAsync(
                    $"{ApiRoutes.FeatureFlags.Base}/{FeatureFlags.ReviewLibraryEnabled}", new { Enabled = true });
                Assert.Equal(HttpStatusCode.OK, on.StatusCode);

                var restored = await client.GetAsync($"{ApiRoutes.ReviewLibrary.Base}/patterns?userId={Guid.NewGuid()}");
                Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
            }
            finally
            {
                await RemoveRowAsync(FeatureFlags.ReviewLibraryEnabled);
            }
        }

        [Fact]
        public async Task Setting_an_unknown_key_is_rejected()
        {
            var client = await _factory.CreateAuthenticatedAdminClientAsync();

            var response = await client.PutAsJsonAsync(
                $"{ApiRoutes.FeatureFlags.Base}/not_a_real_flag", new { Enabled = true });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
