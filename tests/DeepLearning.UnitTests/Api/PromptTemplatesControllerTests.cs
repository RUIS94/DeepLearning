using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ExamConfig.Queries.GetPromptTemplatesByExamType;
using DeepLearning.Domain.Enums;
using DeepLearning.UnitTests.TestInfrastructure;

namespace DeepLearning.UnitTests.Api
{
    [Collection(ApiCollection.Name)]
    public class PromptTemplatesControllerTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public PromptTemplatesControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Create_shared_methodology_template_then_list_round_trips_over_http()
        {
            var client = _factory.CreateClient();

            var createResponse = await client.PostAsJsonAsync(ApiRoutes.PromptTemplates.Base, new
            {
                ExamTypeId = (Guid?)null,
                SubjectCategory = SubjectCategory.translation,
                TemplateType = AiOperationType.grading,
                Layer = TemplateLayer.shared_methodology,
                TemplateContent = "api test content",
                Version = 1,
            });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

            var listResponse = await client.GetAsync(
                $"{ApiRoutes.PromptTemplates.Base}?subjectCategory={SubjectCategory.translation}&templateType={AiOperationType.grading}");
            var items = await listResponse.Content.ReadFromJsonAsync<List<PromptTemplateResultItem>>();

            Assert.Contains(items!, x => x.TemplateContent == "api test content");
        }

        [Fact]
        public async Task Create_returns_400_when_layer_and_scope_are_inconsistent()
        {
            var client = _factory.CreateClient();

            var response = await client.PostAsJsonAsync(ApiRoutes.PromptTemplates.Base, new
            {
                ExamTypeId = (Guid?)null,
                SubjectCategory = (SubjectCategory?)null,
                TemplateType = AiOperationType.grading,
                Layer = TemplateLayer.exam_specific,
                TemplateContent = "invalid",
                Version = 1,
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
