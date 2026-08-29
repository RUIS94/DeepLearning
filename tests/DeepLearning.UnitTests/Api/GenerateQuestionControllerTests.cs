using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateErrorTaxonomy;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateExamType;
using DeepLearning.Application.Features.Questions.Commands.GenerateQuestion;
using DeepLearning.Application.Features.Questions.Queries.GetQuestionById;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Persistence;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
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
            // ILlmClientResolver is swapped for a fixed-JSON fake scoped to this test only —
            // the shared ApiWebApplicationFactory (and every other Api test) keeps using
            // the real, keyed Claude-backed registration from DependencyInjection.cs.
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddScoped<ILlmClientResolver, FakeLlmClientResolver>()))
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
                    services => services.AddScoped<ILlmClientResolver, FakeLlmClientResolver>()))
                .CreateClient();

            var response = await client.PostAsJsonAsync(
                $"{ApiRoutes.Questions.Base}/generate",
                new { ExamTypeId = Guid.NewGuid(), TaskType = TaskType.A, Difficulty = Difficulty.medium, CreatedBy = (Guid?)null });

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Generate_supports_task_b_and_persists_flawed_translation_text_and_seeded_errors()
        {
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddScoped<ILlmClientResolver, FakeTaskBGenerationLlmClientResolver>()))
                .CreateClient();

            var examTypeResponse = await client.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, new
            {
                Code = $"test_{Guid.NewGuid():N}",
                Name = "API Test Exam Type",
                SubjectCategory = SubjectCategory.translation,
            });
            var examType = await examTypeResponse.Content.ReadFromJsonAsync<CreateExamTypeResult>();

            var taxonomyResponse = await client.PostAsJsonAsync(
                ApiRoutes.ErrorTaxonomies.Base.Replace("{examTypeId:guid}", examType!.Id.ToString()),
                new { CategoryKey = FakeTaskBGenerationLlmClient.ErrorCategoryKey, CategoryName = "Distortion", Description = (string?)null, ExampleCases = (string?)null });
            taxonomyResponse.EnsureSuccessStatusCode();

            var generateResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.Questions.Base}/generate",
                new { ExamTypeId = examType.Id, TaskType = TaskType.B, Difficulty = Difficulty.medium, CreatedBy = (Guid?)null });
            Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);
            var generated = await generateResponse.Content.ReadFromJsonAsync<GenerateQuestionResult>();

            var getResponse = await client.GetAsync($"{ApiRoutes.Questions.Base}/{generated!.Id}");
            var question = await getResponse.Content.ReadFromJsonAsync<GetQuestionByIdResult>();

            Assert.NotNull(question!.TaskB);
            Assert.Equal(FakeTaskBGenerationLlmClient.FlawedTranslationText, question.TaskB!.FlawedTranslationText);
            Assert.Single(question.TaskB.SeededErrors);
            Assert.Equal(FakeTaskBGenerationLlmClient.ErrorCategoryKey, question.TaskB.SeededErrors[0].ErrorCategoryKey);
        }

        [Fact]
        public async Task Generate_rejects_a_task_b_response_whose_seeded_error_position_does_not_fit_the_flawed_text()
        {
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddScoped<ILlmClientResolver, FakeTaskBGenerationLlmClientResolverWithOutOfBoundsPosition>()))
                .CreateClient();

            var examTypeResponse = await client.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, new
            {
                Code = $"test_{Guid.NewGuid():N}",
                Name = "API Test Exam Type",
                SubjectCategory = SubjectCategory.translation,
            });
            var examType = await examTypeResponse.Content.ReadFromJsonAsync<CreateExamTypeResult>();

            var taxonomyResponse = await client.PostAsJsonAsync(
                ApiRoutes.ErrorTaxonomies.Base.Replace("{examTypeId:guid}", examType!.Id.ToString()),
                new { CategoryKey = FakeTaskBGenerationLlmClient.ErrorCategoryKey, CategoryName = "Distortion", Description = (string?)null, ExampleCases = (string?)null });
            taxonomyResponse.EnsureSuccessStatusCode();

            var generateResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.Questions.Base}/generate",
                new { ExamTypeId = examType.Id, TaskType = TaskType.B, Difficulty = Difficulty.medium, CreatedBy = (Guid?)null });

            Assert.Equal(HttpStatusCode.ServiceUnavailable, generateResponse.StatusCode);
        }

        [Fact]
        public async Task Generate_omits_difficulty_and_still_succeeds_using_the_default_distribution()
        {
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddScoped<ILlmClientResolver, FakeLlmClientResolver>()))
                .CreateClient();

            var examTypeResponse = await client.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, new
            {
                Code = $"test_{Guid.NewGuid():N}",
                Name = "API Test Exam Type",
                SubjectCategory = SubjectCategory.translation,
            });
            var examType = await examTypeResponse.Content.ReadFromJsonAsync<CreateExamTypeResult>();

            var generateResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.Questions.Base}/generate",
                new { ExamTypeId = examType!.Id, TaskType = TaskType.A, Difficulty = (Difficulty?)null, CreatedBy = (Guid?)null });

            Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);
            var generated = await generateResponse.Content.ReadFromJsonAsync<GenerateQuestionResult>();
            Assert.True(Enum.IsDefined(generated!.Difficulty));
        }

        [Fact]
        public async Task Generate_uses_the_seeded_difficulty_distribution_policy_when_difficulty_is_omitted()
        {
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddScoped<ILlmClientResolver, FakeLlmClientResolver>()))
                .CreateClient();

            var examTypeResponse = await client.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, new
            {
                Code = $"test_{Guid.NewGuid():N}",
                Name = "API Test Exam Type",
                SubjectCategory = SubjectCategory.translation,
            });
            var examType = await examTypeResponse.Content.ReadFromJsonAsync<CreateExamTypeResult>();

            // Degenerate distribution (100% easy) makes the "random" pick deterministic without
            // needing to inject Random into the handler — proves the policy row is actually
            // read and honored, not just that the fallback default doesn't crash.
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await context.GenerationPolicies.AddAsync(new GenerationPolicy
                {
                    Id = Guid.NewGuid(),
                    ExamTypeId = examType!.Id,
                    PolicyKey = "difficulty_distribution",
                    PolicyValue = "{\"easy\": 1.0, \"medium\": 0.0, \"hard\": 0.0}",
                    UpdatedAt = DateTimeOffset.UtcNow,
                });
                await context.SaveChangesAsync();
            }

            var generateResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.Questions.Base}/generate",
                new { ExamTypeId = examType!.Id, TaskType = TaskType.A, Difficulty = (Difficulty?)null, CreatedBy = (Guid?)null });

            Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);
            var generated = await generateResponse.Content.ReadFromJsonAsync<GenerateQuestionResult>();
            Assert.Equal(Difficulty.easy, generated!.Difficulty);
        }

        [Fact]
        public async Task Generate_uses_the_explicit_difficulty_even_when_a_policy_row_exists()
        {
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddScoped<ILlmClientResolver, FakeLlmClientResolver>()))
                .CreateClient();

            var examTypeResponse = await client.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, new
            {
                Code = $"test_{Guid.NewGuid():N}",
                Name = "API Test Exam Type",
                SubjectCategory = SubjectCategory.translation,
            });
            var examType = await examTypeResponse.Content.ReadFromJsonAsync<CreateExamTypeResult>();

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await context.GenerationPolicies.AddAsync(new GenerationPolicy
                {
                    Id = Guid.NewGuid(),
                    ExamTypeId = examType!.Id,
                    PolicyKey = "difficulty_distribution",
                    PolicyValue = "{\"easy\": 1.0, \"medium\": 0.0, \"hard\": 0.0}",
                    UpdatedAt = DateTimeOffset.UtcNow,
                });
                await context.SaveChangesAsync();
            }

            var generateResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.Questions.Base}/generate",
                new { ExamTypeId = examType!.Id, TaskType = TaskType.A, Difficulty = Difficulty.hard, CreatedBy = (Guid?)null });

            Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);
            var generated = await generateResponse.Content.ReadFromJsonAsync<GenerateQuestionResult>();
            Assert.Equal(Difficulty.hard, generated!.Difficulty);
        }
    }
}
