using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.Questions.Commands.ImportUserQuestion;
using DeepLearning.Application.Features.Submissions.Commands.CreateSubmission;
using DeepLearning.Application.Features.Submissions.Queries.GetSubmissionById;
using DeepLearning.Application.Features.Users.Queries.GetUserById;
using DeepLearning.Domain.Enums;
using DeepLearning.UnitTests.TestInfrastructure;

namespace DeepLearning.UnitTests.Api
{
    /// <summary>
    /// Registration/login happen entirely against Supabase Auth (see AGENTS.md's Auth section) —
    /// this backend only validates the JWT that process already issued. ApiWebApplicationFactory
    /// swaps in a test-only symmetric signing key for every test in this collection (Program.cs's
    /// real JwtBearer config points at a real Supabase JWKS endpoint these tests can't reach), so
    /// this still exercises the real JwtBearer handler + CurrentUserService +
    /// EnsureUserProfileMiddleware pipeline exactly as Program.cs wires it.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class JwtAuthenticationTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public JwtAuthenticationTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task A_valid_jwt_creates_a_public_users_profile_row_on_first_authenticated_request()
        {
            var userId = Guid.NewGuid();
            var email = $"{Guid.NewGuid():N}@test.local";
            var client = _factory.CreateAuthenticatedClient(userId, email);

            var response = await client.GetAsync($"{ApiRoutes.Users.Base}/{userId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var fetched = await response.Content.ReadFromJsonAsync<GetUserByIdResult>();
            Assert.Equal(userId, fetched!.Id);
            Assert.Equal(email, fetched.Email);
        }

        [Fact]
        public async Task A_second_request_from_the_same_jwt_does_not_duplicate_or_fail_on_the_already_synced_profile()
        {
            var userId = Guid.NewGuid();
            var client = _factory.CreateAuthenticatedClient(userId);

            var first = await client.GetAsync($"{ApiRoutes.Users.Base}/{userId}");
            var second = await client.GetAsync($"{ApiRoutes.Users.Base}/{userId}");

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        }

        /// <summary>
        /// The anonymous "trust whatever the caller says" fallback this test used to cover was
        /// removed on purpose (see ref/管理员与用户权限隔离_策划书.md Phase 1) — it let anyone read
        /// or write any user's data by guessing their GUID. Every controller action now requires
        /// authentication via Program.cs's global AuthorizeFilter.
        /// </summary>
        [Fact]
        public async Task Requests_without_a_jwt_are_rejected()
        {
            var client = _factory.CreateClient();
            var userId = await _factory.SeedUserAsync();

            var response = await client.GetAsync($"{ApiRoutes.Users.Base}/{userId}");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        /// <summary>
        /// A submission is always attributed to the caller's own JWT identity — there is no longer
        /// a body-supplied UserId field to override at all (see ref/管理员与用户权限隔离_策划书.md
        /// Phase 1: CreateSubmissionRequest dropped the field entirely rather than merely ignoring it).
        /// </summary>
        [Fact]
        public async Task A_created_submission_is_always_attributed_to_the_caller_s_own_jwt_identity()
        {
            var authorId = Guid.NewGuid();
            var authorClient = _factory.CreateAuthenticatedClient(authorId);
            var questionResponse = await authorClient.PostAsJsonAsync(ApiRoutes.Questions.Base, new
            {
                TaskType = TaskType.A,
                Difficulty = Difficulty.medium,
                Title = "JWT Attribution Test Question",
                Brief = (string?)null,
                SourceText = "Some source text.",
                FlawedTranslationText = (string?)null,
                WordCount = 50,
                Visibility = Visibility.Private,
                MeaningCheckpoints = Array.Empty<object>(),
                SeededErrors = Array.Empty<object>(),
            });
            questionResponse.EnsureSuccessStatusCode();
            var question = await questionResponse.Content.ReadFromJsonAsync<ImportUserQuestionResult>();

            var jwtUserId = Guid.NewGuid();
            var authenticatedClient = _factory.CreateAuthenticatedClient(jwtUserId);

            var createResponse = await authenticatedClient.PostAsJsonAsync(ApiRoutes.Submissions.Base, new
            {
                QuestionId = question!.Id,
                TaskType = TaskType.A,
                Content = "\"my translation\"",
            });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<CreateSubmissionResult>();

            var getResponse = await authenticatedClient.GetAsync($"{ApiRoutes.Submissions.Base}/{created!.Id}");
            var fetched = await getResponse.Content.ReadFromJsonAsync<GetSubmissionByIdResult>();

            Assert.Equal(jwtUserId, fetched!.UserId);
        }
    }
}
