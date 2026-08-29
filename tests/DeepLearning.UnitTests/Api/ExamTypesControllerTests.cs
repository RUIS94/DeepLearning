using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateExamType;
using DeepLearning.Application.Features.ExamConfig.Queries.GetExamTypeById;
using DeepLearning.Domain.Enums;
using DeepLearning.UnitTests.TestInfrastructure;

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
            var client = _factory.CreateClient();
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
            var client = _factory.CreateClient();

            var response = await client.GetAsync($"{ApiRoutes.ExamTypes.Base}/{Guid.NewGuid()}");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Create_returns_400_when_code_is_missing()
        {
            var client = _factory.CreateClient();
            var request = new { Code = "", Name = "Missing Code", SubjectCategory = SubjectCategory.translation };

            var response = await client.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Create_returns_409_when_code_already_exists()
        {
            var client = _factory.CreateClient();
            var request = new { Code = $"test_{Guid.NewGuid():N}", Name = "Duplicate", SubjectCategory = SubjectCategory.translation };

            var first = await client.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, request);
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);

            var second = await client.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, request);
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        }
    }
}
