using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateExamType;
using DeepLearning.Application.Features.ExamConfig.Queries.GetAssessmentDimensionsByExamType;
using DeepLearning.Domain.Enums;
using DeepLearning.UnitTests.TestInfrastructure;

namespace DeepLearning.UnitTests.Api
{
    [Collection(ApiCollection.Name)]
    public class AssessmentDimensionsControllerTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public AssessmentDimensionsControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<Guid> CreateExamTypeAsync(HttpClient client)
        {
            var response = await client.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, new
            {
                Code = $"test_{Guid.NewGuid():N}",
                Name = "API Test Exam Type",
                SubjectCategory = SubjectCategory.translation,
            });
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<CreateExamTypeResult>();
            return result!.Id;
        }

        [Fact]
        public async Task Create_then_list_round_trips_over_http()
        {
            var client = _factory.CreateClient();
            var examTypeId = await CreateExamTypeAsync(client);
            var baseUrl = ApiRoutes.AssessmentDimensions.Base.Replace("{examTypeId:guid}", examTypeId.ToString());

            var createResponse = await client.PostAsJsonAsync(baseUrl, new
            {
                DimensionKey = "meaning_transfer",
                DimensionName = "Meaning transfer",
                ScaleType = ScaleType.band_1_5,
                PassThreshold = "Band 2 or above",
                ApplicableTaskType = TaskType.A,
                LevelDescriptions = "{\"1\":\"...\"}",
                RubricVersion = "2024-02",
                EffectiveFrom = DateTimeOffset.UtcNow,
                EffectiveTo = (DateTimeOffset?)null,
                SourceReference = (string?)null,
            });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            var listResponse = await client.GetAsync(baseUrl);
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

            var items = await listResponse.Content.ReadFromJsonAsync<List<AssessmentDimensionResultItem>>();
            Assert.Single(items!);
        }

        [Fact]
        public async Task Create_returns_404_for_unknown_exam_type()
        {
            var client = _factory.CreateClient();
            var baseUrl = ApiRoutes.AssessmentDimensions.Base.Replace("{examTypeId:guid}", Guid.NewGuid().ToString());

            var response = await client.PostAsJsonAsync(baseUrl, new
            {
                DimensionKey = "meaning_transfer",
                DimensionName = "Meaning transfer",
                ScaleType = ScaleType.band_1_5,
                LevelDescriptions = "{\"1\":\"...\"}",
                RubricVersion = "2024-02",
                EffectiveFrom = DateTimeOffset.UtcNow,
            });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
