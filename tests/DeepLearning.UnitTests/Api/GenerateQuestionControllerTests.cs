using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateExamType;
using DeepLearning.Application.Features.Questions.Commands.GenerateQuestion;
using DeepLearning.Application.Features.Questions.Queries.GetQuestionById;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Enums;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DeepLearning.UnitTests.Api
{
    [Collection(ApiCollection.Name)]
    public class GenerateQuestionControllerTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public GenerateQuestionControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Generate_persists_the_llm_response_fields_and_returns_them()
        {
            // ILlmClient is swapped for a fixed-JSON fake scoped to this test only —
            // the shared ApiWebApplicationFactory (and every other Api test) keeps using
            // the real, keyed Claude-backed registration from DependencyInjection.cs.
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddScoped<ILlmClient, FakeLlmClient>()))
                .CreateClient();

            var examTypeResponse = await client.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, new
            {
                Code = $"test_{Guid.NewGuid():N}",
                Name = "API Test Exam Type",
                SubjectCategory = SubjectCategory.translation,
            });
            examTypeResponse.EnsureSuccessStatusCode();
            var examType = await examTypeResponse.Content.ReadFromJsonAsync<CreateExamTypeResult>();

            var generateResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.Questions.Base}/generate",
                new { ExamTypeId = examType!.Id, TaskType = TaskType.A, Difficulty = Difficulty.medium, CreatedBy = (Guid?)null });
            Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);

            var generated = await generateResponse.Content.ReadFromJsonAsync<GenerateQuestionResult>();
            Assert.Equal(FakeLlmClient.FixedTitle, generated!.Title);

            var getResponse = await client.GetAsync($"{ApiRoutes.Questions.Base}/{generated.Id}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            var question = await getResponse.Content.ReadFromJsonAsync<GetQuestionByIdResult>();
            Assert.Equal(FakeLlmClient.FixedTitle, question!.Title);
            Assert.Equal(FakeLlmClient.FixedSourceText, question.SourceText);
            Assert.Equal(QuestionOrigin.ai_generated, question.Origin);
            Assert.Single(question.MeaningCheckpoints);
        }

        [Fact]
        public async Task Generate_returns_404_for_an_unknown_exam_type()
        {
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddScoped<ILlmClient, FakeLlmClient>()))
                .CreateClient();

            var response = await client.PostAsJsonAsync(
                $"{ApiRoutes.Questions.Base}/generate",
                new { ExamTypeId = Guid.NewGuid(), TaskType = TaskType.A, Difficulty = Difficulty.medium, CreatedBy = (Guid?)null });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
