using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.Users.Queries.GetUserById;
using DeepLearning.UnitTests.TestInfrastructure;

namespace DeepLearning.UnitTests.Api
{
    /// <summary>
    /// Registration/login moved to Supabase Auth (see AGENTS.md's Auth section) — there is no
    /// longer a POST /users endpoint on this backend at all, so this file only covers the read
    /// side. Auth-driven profile creation (EnsureUserProfileMiddleware) is covered by
    /// JwtAuthenticationTests.cs instead.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class UsersControllerTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public UsersControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Get_by_id_returns_a_user_seeded_directly_in_the_database_when_the_caller_is_that_user()
        {
            var userId = await _factory.SeedUserAsync();
            var client = _factory.CreateAuthenticatedClient(userId);

            var response = await client.GetAsync($"{ApiRoutes.Users.Base}/{userId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var fetched = await response.Content.ReadFromJsonAsync<GetUserByIdResult>();
            Assert.Equal(userId, fetched!.Id);
        }

        [Fact]
        public async Task Get_by_id_returns_404_for_unknown_id_when_the_caller_is_an_admin()
        {
            var client = await _factory.CreateAuthenticatedAdminClientAsync();

            var response = await client.GetAsync($"{ApiRoutes.Users.Base}/{Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Get_by_id_returns_403_for_a_different_user_s_profile()
        {
            var targetUserId = await _factory.SeedUserAsync();
            var client = _factory.CreateAuthenticatedClient();

            var response = await client.GetAsync($"{ApiRoutes.Users.Base}/{targetUserId}");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Get_by_id_allows_an_admin_to_read_another_user_s_profile()
        {
            var targetUserId = await _factory.SeedUserAsync();
            var adminClient = await _factory.CreateAuthenticatedAdminClientAsync();

            var response = await adminClient.GetAsync($"{ApiRoutes.Users.Base}/{targetUserId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Get_self_returns_the_caller_s_own_profile()
        {
            var userId = await _factory.SeedUserAsync();
            var client = _factory.CreateAuthenticatedClient(userId);

            var response = await client.GetAsync($"{ApiRoutes.Users.Base}/me");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var fetched = await response.Content.ReadFromJsonAsync<GetUserByIdResult>();
            Assert.Equal(userId, fetched!.Id);
        }
    }
}
