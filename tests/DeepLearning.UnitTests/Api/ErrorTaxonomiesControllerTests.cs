using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateExamType;
using DeepLearning.Application.Features.ExamConfig.Queries.GetErrorTaxonomiesByExamType;
using DeepLearning.Domain.Enums;
using DeepLearning.UnitTests.TestInfrastructure;

namespace DeepLearning.UnitTests.Api
{
    [Collection(ApiCollection.Name)]
    public class ErrorTaxonomiesControllerTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public ErrorTaxonomiesControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Create_then_list_round_trips_over_http()
        {
            var client = _factory.CreateClient();
            var examTypeResponse = await client.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, new
            {
                Code = $"test_{Guid.NewGuid():N}",
                Name = "API Test Exam Type",
                SubjectCategory = SubjectCategory.translation,
            });
            examTypeResponse.EnsureSuccessStatusCode();
            var examType = await examTypeResponse.Content.ReadFromJsonAsync<CreateExamTypeResult>();
            var baseUrl = ApiRoutes.ErrorTaxonomies.Base.Replace("{examTypeId:guid}", examType!.Id.ToString());

            var createResponse = await client.PostAsJsonAsync(baseUrl, new
            {
                CategoryKey = "distortion",
                CategoryName = "Distortion",
                Description = (string?)null,
                ExampleCases = (string?)null,
            });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            var listResponse = await client.GetAsync(baseUrl);
            var items = await listResponse.Content.ReadFromJsonAsync<List<ErrorTaxonomyResultItem>>();

            Assert.Single(items!);
            Assert.Equal("distortion", items![0].CategoryKey);
        }
    }
}
