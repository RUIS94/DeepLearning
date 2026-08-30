using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.Questions.Commands.ImportUserQuestion;
using DeepLearning.Application.Features.Submissions.Commands.CreateSubmission;
using DeepLearning.Application.Features.Submissions.Queries.GetSubmissionById;
using DeepLearning.Application.Features.Users.Queries.GetUserById;
using DeepLearning.Domain.Enums;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace DeepLearning.UnitTests.Api
{
    /// <summary>
    /// Registration/login happen entirely against Supabase Auth (see AGENTS.md's Auth section) —
    /// this backend only validates the JWT that process already issued. Program.cs configures
    /// JwtBearer against a real Supabase project's JWKS endpoint via Authority, which these tests
    /// can't reach; instead each test here swaps in a test-only symmetric signing key via
    /// PostConfigure&lt;JwtBearerOptions&gt; so a locally-signed token can be validated without any
    /// network call, while still exercising the real JwtBearer handler + CurrentUserService +
    /// EnsureUserProfileMiddleware pipeline exactly as Program.cs wires it.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class JwtAuthenticationTests
    {
        private const string TestIssuer = "https://test-project.supabase.co/auth/v1";

        private static readonly SymmetricSecurityKey TestSigningKey =
            new(Encoding.UTF8.GetBytes("test-only-hmac-signing-key-at-least-32-bytes-long"));

        private readonly ApiWebApplicationFactory _factory;

        public JwtAuthenticationTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private HttpClient CreateClientWithTestSigningKey()
            => _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                    services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                    {
                        options.Authority = null;
                        options.RequireHttpsMetadata = false;
                        options.MapInboundClaims = false;
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidIssuer = TestIssuer,
                            ValidateAudience = true,
                            ValidAudience = "authenticated",
                            ValidateLifetime = true,
                            IssuerSigningKey = TestSigningKey,
                        };
                    })))
                .CreateClient();

        private static string CreateJwt(Guid userId, string email)
        {
            var handler = new JwtSecurityTokenHandler();
            var token = new JwtSecurityToken(
                issuer: TestIssuer,
                audience: "authenticated",
                claims: [new Claim("sub", userId.ToString()), new Claim("email", email)],
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: new SigningCredentials(TestSigningKey, SecurityAlgorithms.HmacSha256));
            return handler.WriteToken(token);
        }

        [Fact]
        public async Task A_valid_jwt_creates_a_public_users_profile_row_on_first_authenticated_request()
        {
            var client = CreateClientWithTestSigningKey();
            var userId = Guid.NewGuid();
            var email = $"{Guid.NewGuid():N}@test.local";
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateJwt(userId, email));

            var response = await client.GetAsync($"{ApiRoutes.Users.Base}/{userId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var fetched = await response.Content.ReadFromJsonAsync<GetUserByIdResult>();
            Assert.Equal(userId, fetched!.Id);
            Assert.Equal(email, fetched.Email);
        }

        [Fact]
        public async Task A_second_request_from_the_same_jwt_does_not_duplicate_or_fail_on_the_already_synced_profile()
        {
            var client = CreateClientWithTestSigningKey();
            var userId = Guid.NewGuid();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", CreateJwt(userId, $"{Guid.NewGuid():N}@test.local"));

            var first = await client.GetAsync($"{ApiRoutes.Users.Base}/{userId}");
            var second = await client.GetAsync($"{ApiRoutes.Users.Base}/{userId}");

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        }

        [Fact]
        public async Task Requests_without_a_jwt_still_work_using_the_body_supplied_user_id()
        {
            var client = _factory.CreateClient();
            var userId = await _factory.SeedUserAsync();

            var response = await client.GetAsync($"{ApiRoutes.Users.Base}/{userId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        /// <summary>
        /// The whole point of making auth opt-in (design decision, see AGENTS.md): a valid JWT's
        /// identity always wins over whatever UserId a caller puts in the request body.
        /// </summary>
        [Fact]
        public async Task A_jwt_identity_overrides_an_explicit_but_different_user_id_in_the_request_body()
        {
            var anonymousClient = _factory.CreateClient();
            var questionResponse = await anonymousClient.PostAsJsonAsync(ApiRoutes.Questions.Base, new
            {
                TaskType = TaskType.A,
                Difficulty = Difficulty.medium,
                Title = "JWT Override Test Question",
                Brief = (string?)null,
                SourceText = "Some source text.",
                FlawedTranslationText = (string?)null,
                WordCount = 50,
                CreatedBy = (Guid?)null,
                Visibility = Visibility.Private,
                MeaningCheckpoints = Array.Empty<object>(),
                SeededErrors = Array.Empty<object>(),
            });
            questionResponse.EnsureSuccessStatusCode();
            var question = await questionResponse.Content.ReadFromJsonAsync<ImportUserQuestionResult>();

            var bodyUserId = await _factory.SeedUserAsync();
            var jwtUserId = Guid.NewGuid();

            var authenticatedClient = CreateClientWithTestSigningKey();
            authenticatedClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", CreateJwt(jwtUserId, $"{Guid.NewGuid():N}@test.local"));

            var createResponse = await authenticatedClient.PostAsJsonAsync(ApiRoutes.Submissions.Base, new
            {
                QuestionId = question!.Id,
                UserId = bodyUserId, // deliberately NOT the JWT's user — should be ignored
                TaskType = TaskType.A,
                Content = "\"my translation\"",
            });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<CreateSubmissionResult>();

            var getResponse = await authenticatedClient.GetAsync($"{ApiRoutes.Submissions.Base}/{created!.Id}");
            var fetched = await getResponse.Content.ReadFromJsonAsync<GetSubmissionByIdResult>();

            Assert.Equal(jwtUserId, fetched!.UserId);
            Assert.NotEqual(bodyUserId, fetched.UserId);
        }
    }
}
