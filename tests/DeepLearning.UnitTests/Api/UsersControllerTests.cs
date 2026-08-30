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
        public async Task Get_by_id_returns_a_user_seeded_directly_in_the_database()
        {
            var client = _factory.CreateClient();
            var userId = await _factory.SeedUserAsync();

            var response = await client.GetAsync($"{ApiRoutes.Users.Base}/{userId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var fetched = await response.Content.ReadFromJsonAsync<GetUserByIdResult>();
            Assert.Equal(userId, fetched!.Id);
        }

        [Fact]
        public async Task Get_by_id_returns_404_for_unknown_id()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync($"{ApiRoutes.Users.Base}/{Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
