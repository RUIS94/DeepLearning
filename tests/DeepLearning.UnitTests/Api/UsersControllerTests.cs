using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.Users.Commands.RegisterUser;
using DeepLearning.UnitTests.TestInfrastructure;

namespace DeepLearning.UnitTests.Api
{
    [Collection(ApiCollection.Name)]
    public class UsersControllerTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public UsersControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Register_then_get_by_id_round_trips_over_http()
        {
            var client = _factory.CreateClient();
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var request = new
            {
                Username = $"user_{suffix}",
                Email = $"user_{suffix}@example.com",
                Password = "correct horse battery staple",
                DisplayName = "Test User",
            };

            var createResponse = await client.PostAsJsonAsync(ApiRoutes.Users.Base, request);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            var created = await createResponse.Content.ReadFromJsonAsync<RegisterUserResult>();
            Assert.NotNull(created);

            var getResponse = await client.GetAsync($"{ApiRoutes.Users.Base}/{created!.Id}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        }

        [Fact]
        public async Task Register_returns_400_when_password_too_short()
        {
            var client = _factory.CreateClient();
            var request = new { Username = "shortpw", Email = "shortpw@example.com", Password = "abc", DisplayName = (string?)null };

            var response = await client.PostAsJsonAsync(ApiRoutes.Users.Base, request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Register_returns_409_for_duplicate_username()
        {
            var client = _factory.CreateClient();
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var request = new
            {
                Username = $"dup_{suffix}",
                Email = $"dup_{suffix}@example.com",
                Password = "correct horse battery staple",
                DisplayName = (string?)null,
            };

            var first = await client.PostAsJsonAsync(ApiRoutes.Users.Base, request);
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);

            var second = await client.PostAsJsonAsync(ApiRoutes.Users.Base, request with { Email = $"other_{suffix}@example.com" });
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        }
    }
}
