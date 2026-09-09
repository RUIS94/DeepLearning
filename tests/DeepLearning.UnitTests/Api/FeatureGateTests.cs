using System.Net;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Common;
using DeepLearning.Domain.Entities;
using DeepLearning.Infrastructure.Persistence;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DeepLearning.UnitTests.Api
{
    /// <summary>
    /// [FeatureGate("...")] on the 题库 / 复习库 controllers reads feature_flags (design doc §六 /
    /// §11.2 Step 10). The Testcontainers DB has no seed row, so a key with no row must behave
    /// exactly as before (default on); an explicit disabled row turns the whole controller into
    /// 404. The factory registers the flag service with a zero-length cache so these reads are
    /// always fresh across the shared ApiCollection.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class FeatureGateTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public FeatureGateTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task SetFlagAsync(string key, bool? enabled)
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var existing = await context.FeatureFlags.FirstOrDefaultAsync(x => x.Key == key);

            if (enabled is null)
            {
                if (existing is not null)
                {
                    context.FeatureFlags.Remove(existing);
                }
            }
            else if (existing is null)
            {
                await context.FeatureFlags.AddAsync(new FeatureFlag
                {
                    Id = Guid.NewGuid(),
                    Key = key,
                    Enabled = enabled.Value,
                    Scope = "global",
                    UpdatedAt = DateTimeOffset.UtcNow,
                });
            }
            else
            {
                existing.Enabled = enabled.Value;
            }

            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task Review_library_endpoint_is_reachable_when_the_flag_row_is_absent()
        {
            await SetFlagAsync(FeatureFlags.ReviewLibraryEnabled, null);
            var client = _factory.CreateClient();

            var response = await client.GetAsync($"{ApiRoutes.ReviewLibrary.Base}/patterns?userId={Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Review_library_endpoint_returns_404_when_its_flag_is_disabled()
        {
            await SetFlagAsync(FeatureFlags.ReviewLibraryEnabled, false);
            try
            {
                var client = _factory.CreateClient();

                var response = await client.GetAsync($"{ApiRoutes.ReviewLibrary.Base}/vocab?userId={Guid.NewGuid()}");

                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }
            finally
            {
                await SetFlagAsync(FeatureFlags.ReviewLibraryEnabled, null);
            }
        }

        [Fact]
        public async Task Question_bank_categories_endpoint_returns_404_when_its_flag_is_disabled()
        {
            await SetFlagAsync(FeatureFlags.QuestionBankEnabled, false);
            try
            {
                var client = _factory.CreateClient();

                var response = await client.GetAsync(ApiRoutes.QuestionBankCategories.Base);

                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            }
            finally
            {
                await SetFlagAsync(FeatureFlags.QuestionBankEnabled, null);
            }
        }
    }
}
