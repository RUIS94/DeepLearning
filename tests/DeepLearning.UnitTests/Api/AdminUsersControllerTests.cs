using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Common;
using DeepLearning.Application.Features.Users.Commands.SetUserFeatureOverride;
using DeepLearning.Application.Features.Users.Commands.UpdateUserRole;
using DeepLearning.Application.Features.Users.Queries.ListUserFeatureOverrides;
using DeepLearning.Application.Features.Users.Queries.ListUsers;
using DeepLearning.Domain.Enums;
using DeepLearning.UnitTests.TestInfrastructure;

namespace DeepLearning.UnitTests.Api
{
    /// <summary>
    /// A3/A4 (ref/管理员与用户权限隔离_策划书.md) — net-new module, no prior test coverage existed
    /// since neither "list every user" nor "change a role" existed in the API before this.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class AdminUsersControllerTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public AdminUsersControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task List_returns_403_for_a_non_admin_caller()
        {
            var client = _factory.CreateAuthenticatedClient();

            var response = await client.GetAsync(ApiRoutes.AdminUsers.Base);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task List_returns_401_for_an_anonymous_caller()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync(ApiRoutes.AdminUsers.Base);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task List_includes_a_seeded_user_for_an_admin_caller()
        {
            var userId = await _factory.SeedUserAsync();
            var admin = await _factory.CreateAuthenticatedAdminClientAsync();

            var response = await admin.GetAsync($"{ApiRoutes.AdminUsers.Base}?page=1&pageSize=200");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<ListUsersResult>();
            Assert.Contains(result!.Items, u => u.Id == userId);
        }

        [Fact]
        public async Task UpdateRole_promotes_a_user_who_can_then_use_admin_endpoints()
        {
            var userId = await _factory.SeedUserAsync();
            var admin = await _factory.CreateAuthenticatedAdminClientAsync();

            var promote = await admin.PutAsJsonAsync(
                $"{ApiRoutes.AdminUsers.Base}/{userId}/role", new { Role = UserRole.admin });
            Assert.Equal(HttpStatusCode.OK, promote.StatusCode);
            var promoted = await promote.Content.ReadFromJsonAsync<UpdateUserRoleResult>();
            Assert.Equal(UserRole.admin, promoted!.Role);

            // The promotion takes effect on this user's very next request — no re-login needed
            // (EnsureUserProfileMiddleware mirrors the DB role into a claim every request).
            var nowAdminClient = _factory.CreateAuthenticatedClient(userId);
            var response = await nowAdminClient.GetAsync(ApiRoutes.AdminUsers.Base);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task SetFeatureOverride_grants_a_feature_to_one_user_independent_of_the_global_flag()
        {
            var userId = await _factory.SeedUserAsync();
            var admin = await _factory.CreateAuthenticatedAdminClientAsync();

            // Disable the flag globally first.
            var disableGlobal = await admin.PutAsJsonAsync(
                $"{ApiRoutes.FeatureFlags.Base}/{FeatureFlags.ReviewLibraryEnabled}", new { Enabled = false });
            Assert.Equal(HttpStatusCode.OK, disableGlobal.StatusCode);

            try
            {
                // Without an override, this user is gated off like everyone else.
                var gatedClient = _factory.CreateAuthenticatedClient(userId);
                var gated = await gatedClient.GetAsync($"{ApiRoutes.ReviewLibrary.Base}/patterns");
                Assert.Equal(HttpStatusCode.NotFound, gated.StatusCode);

                var setOverride = await admin.PutAsJsonAsync(
                    $"{ApiRoutes.AdminUsers.Base}/{userId}/features/{FeatureFlags.ReviewLibraryEnabled}",
                    new { Enabled = true });
                Assert.Equal(HttpStatusCode.OK, setOverride.StatusCode);
                var overrideResult = await setOverride.Content.ReadFromJsonAsync<SetUserFeatureOverrideResult>();
                Assert.True(overrideResult!.Enabled);

                var overriddenClient = _factory.CreateAuthenticatedClient(userId);
                var allowed = await overriddenClient.GetAsync($"{ApiRoutes.ReviewLibrary.Base}/patterns");
                Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

                // A different user with no override is still gated off — this isn't a global flip.
                var otherClient = _factory.CreateAuthenticatedClient();
                var stillGated = await otherClient.GetAsync($"{ApiRoutes.ReviewLibrary.Base}/patterns");
                Assert.Equal(HttpStatusCode.NotFound, stillGated.StatusCode);

                // Clearing the override (Enabled=null) falls back to the still-disabled global flag.
                var clearOverride = await admin.PutAsJsonAsync(
                    $"{ApiRoutes.AdminUsers.Base}/{userId}/features/{FeatureFlags.ReviewLibraryEnabled}",
                    new { Enabled = (bool?)null });
                Assert.Equal(HttpStatusCode.OK, clearOverride.StatusCode);

                var afterClear = await _factory.CreateAuthenticatedClient(userId)
                    .GetAsync($"{ApiRoutes.ReviewLibrary.Base}/patterns");
                Assert.Equal(HttpStatusCode.NotFound, afterClear.StatusCode);
            }
            finally
            {
                await admin.PutAsJsonAsync($"{ApiRoutes.FeatureFlags.Base}/{FeatureFlags.ReviewLibraryEnabled}", new { Enabled = true });
            }
        }

        [Fact]
        public async Task ListFeatureOverrides_reflects_a_previously_set_override()
        {
            var userId = await _factory.SeedUserAsync();
            var admin = await _factory.CreateAuthenticatedAdminClientAsync();

            await admin.PutAsJsonAsync(
                $"{ApiRoutes.AdminUsers.Base}/{userId}/features/{FeatureFlags.ReviewLibraryEnabled}",
                new { Enabled = false });

            var response = await admin.GetAsync($"{ApiRoutes.AdminUsers.Base}/{userId}/features");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var items = await response.Content.ReadFromJsonAsync<List<ListUserFeatureOverridesResultItem>>();
            var row = Assert.Single(items!, x => x.FeatureKey == FeatureFlags.ReviewLibraryEnabled);
            Assert.False(row.Enabled);
        }

        [Fact]
        public async Task SetFeatureOverride_returns_400_for_an_unknown_feature_key()
        {
            var userId = await _factory.SeedUserAsync();
            var admin = await _factory.CreateAuthenticatedAdminClientAsync();

            var response = await admin.PutAsJsonAsync(
                $"{ApiRoutes.AdminUsers.Base}/{userId}/features/not_a_real_flag", new { Enabled = true });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
