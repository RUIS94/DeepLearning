using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateExamType;
using DeepLearning.Application.Features.ExamConfig.Commands.SetExamTypeActivation;
using DeepLearning.Application.Features.ExamConfig.Queries.GetExamTypeById;
using DeepLearning.Application.Features.ExamConfig.Queries.ListMyExamTypeActivations;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Persistence;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DeepLearning.UnitTests.Api
{
    [Collection(ApiCollection.Name)]
    public class ExamTypesControllerTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public ExamTypesControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Create_then_get_by_id_round_trips_over_http()
        {
            var client = await _factory.CreateAuthenticatedAdminClientAsync();
            var request = new
            {
                Code = $"test_{Guid.NewGuid():N}",
                Name = "API Test Exam Type",
                SubjectCategory = SubjectCategory.translation,
                SourceLanguage = "en",
                TargetLanguage = "zh",
                GradeLevel = (string?)null,
                Description = (string?)null,
            };

            var createResponse = await client.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, request);
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            var created = await createResponse.Content.ReadFromJsonAsync<CreateExamTypeResult>();
            Assert.NotNull(created);

            var getResponse = await client.GetAsync($"{ApiRoutes.ExamTypes.Base}/{created!.Id}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            var fetched = await getResponse.Content.ReadFromJsonAsync<GetExamTypeByIdResult>();
            Assert.Equal(request.Code, fetched!.Code);
        }

        [Fact]
        public async Task Get_by_id_returns_404_for_unknown_id()
        {
            var client = await _factory.CreateAuthenticatedAdminClientAsync();

            var response = await client.GetAsync($"{ApiRoutes.ExamTypes.Base}/{Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Create_returns_400_when_code_is_missing()
        {
            var client = await _factory.CreateAuthenticatedAdminClientAsync();
            var request = new { Code = "", Name = "Missing Code", SubjectCategory = SubjectCategory.translation };

            var response = await client.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Create_returns_403_for_a_non_admin_caller()
        {
            var client = _factory.CreateAuthenticatedClient();
            var request = new { Code = $"test_{Guid.NewGuid():N}", Name = "Non-admin attempt", SubjectCategory = SubjectCategory.translation };

            var response = await client.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, request);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Create_returns_409_when_code_already_exists()
        {
            var client = await _factory.CreateAuthenticatedAdminClientAsync();
            var request = new { Code = $"test_{Guid.NewGuid():N}", Name = "Duplicate", SubjectCategory = SubjectCategory.translation };

            var first = await client.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, request);
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);

            var second = await client.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, request);
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        }

        private async Task<Guid> CreateExamTypeAsync(HttpClient adminClient)
        {
            var response = await adminClient.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, new
            {
                Code = $"test_{Guid.NewGuid():N}",
                Name = "API Test Exam Type",
                SubjectCategory = SubjectCategory.translation,
            });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<CreateExamTypeResult>())!.Id;
        }

        [Fact]
        public async Task SetActivation_lets_a_user_activate_a_globally_active_exam_type()
        {
            var admin = await _factory.CreateAuthenticatedAdminClientAsync();
            var examTypeId = await CreateExamTypeAsync(admin);
            var client = _factory.CreateAuthenticatedClient();

            var response = await client.PutAsJsonAsync(
                $"{ApiRoutes.ExamTypes.Base}/{examTypeId}/activation", new { IsActive = true });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<SetExamTypeActivationResult>();
            Assert.True(result!.IsActive);
        }

        [Fact]
        public async Task ListMine_reflects_activation_state_and_is_per_user()
        {
            var admin = await _factory.CreateAuthenticatedAdminClientAsync();
            var examTypeId = await CreateExamTypeAsync(admin);
            var client = _factory.CreateAuthenticatedClient();
            var otherClient = _factory.CreateAuthenticatedClient();

            await client.PutAsJsonAsync($"{ApiRoutes.ExamTypes.Base}/{examTypeId}/activation", new { IsActive = true });

            var mine = await client.GetFromJsonAsync<List<ExamTypeActivationResultItem>>($"{ApiRoutes.ExamTypes.Base}/mine");
            var mineRow = Assert.Single(mine!, x => x.ExamTypeId == examTypeId);
            Assert.True(mineRow.IsActivatedByMe);

            var others = await otherClient.GetFromJsonAsync<List<ExamTypeActivationResultItem>>($"{ApiRoutes.ExamTypes.Base}/mine");
            var othersRow = Assert.Single(others!, x => x.ExamTypeId == examTypeId);
            Assert.False(othersRow.IsActivatedByMe);
        }

        [Fact]
        public async Task SetActivation_can_deactivate_after_activating()
        {
            var admin = await _factory.CreateAuthenticatedAdminClientAsync();
            var examTypeId = await CreateExamTypeAsync(admin);
            var client = _factory.CreateAuthenticatedClient();

            await client.PutAsJsonAsync($"{ApiRoutes.ExamTypes.Base}/{examTypeId}/activation", new { IsActive = true });
            var response = await client.PutAsJsonAsync($"{ApiRoutes.ExamTypes.Base}/{examTypeId}/activation", new { IsActive = false });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var mine = await client.GetFromJsonAsync<List<ExamTypeActivationResultItem>>($"{ApiRoutes.ExamTypes.Base}/mine");
            // "mine" lists every globally-active exam type (not just activated ones) — the row is
            // still there, just flipped back to not-activated.
            var row = Assert.Single(mine!, x => x.ExamTypeId == examTypeId);
            Assert.False(row.IsActivatedByMe);
        }

        [Fact]
        public async Task SetActivation_returns_404_for_an_unknown_exam_type()
        {
            var client = _factory.CreateAuthenticatedClient();

            var response = await client.PutAsJsonAsync(
                $"{ApiRoutes.ExamTypes.Base}/{Guid.NewGuid()}/activation", new { IsActive = true });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task SetActivation_returns_400_when_the_exam_type_is_globally_inactive()
        {
            // No admin endpoint deactivates an exam type today (CreateExamTypeCommandHandler
            // always sets IsActive=true) — seed the globally-inactive row directly, same
            // convention as ConfigCrudTests' StandardOverride seeding.
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var examType = new ExamType
            {
                Id = Guid.NewGuid(),
                Code = $"inactive_{Guid.NewGuid():N}",
                Name = "Inactive Exam Type",
                SubjectCategory = SubjectCategory.translation,
                IsActive = false,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            context.ExamTypes.Add(examType);
            await context.SaveChangesAsync();

            var client = _factory.CreateAuthenticatedClient();

            var response = await client.PutAsJsonAsync(
                $"{ApiRoutes.ExamTypes.Base}/{examType.Id}/activation", new { IsActive = true });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
