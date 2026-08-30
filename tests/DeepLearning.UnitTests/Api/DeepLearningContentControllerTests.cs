using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateAssessmentDimension;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateExamType;
using DeepLearning.Application.Features.Questions.Commands.GenerateDeepLearningContent;
using DeepLearning.Application.Features.Questions.Commands.ImportUserQuestion;
using DeepLearning.Application.Features.Questions.Queries.GetDeepLearningContentByQuestionId;
using DeepLearning.Application.Features.Submissions.Commands.CreateSubmission;
using DeepLearning.Application.Features.Submissions.Commands.GradeSubmission;
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
    /// <summary>
    /// Step 7 (design doc §11.2): reference_translations独立调用生成、sentence_patterns/
    /// vocab_expressions, plus the isolation requirement §11.2 explicitly calls out — "断言
    /// '参考译文生成请求'的实际payload里不包含评分相关上下文,验证隔离机制真正生效而非只是文档描述".
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class DeepLearningContentControllerTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public DeepLearningContentControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private const string GradingMarker = "GRADING_CONTAMINATION_MARKER_9f3c";
        private const string DeepLearningMarkerTemplate = "DEEP_LEARNING_SOURCE_MARKER: {{ source_text }}";

        private async Task<(Guid ExamTypeId, Guid QuestionId, Guid UserId)> SeedExamTypeQuestionAndUserAsync(HttpClient client)
        {
            var examTypeResponse = await client.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, new
            {
                Code = $"test_{Guid.NewGuid():N}",
                Name = "API Test Exam Type",
                SubjectCategory = SubjectCategory.translation,
            });
            examTypeResponse.EnsureSuccessStatusCode();
            var examType = await examTypeResponse.Content.ReadFromJsonAsync<CreateExamTypeResult>();

            var taxonomyResponse = await client.PostAsJsonAsync(
                ApiRoutes.ErrorTaxonomies.Base.Replace("{examTypeId:guid}", examType!.Id.ToString()),
                new { CategoryKey = FakeGradingLlmClient.ErrorCategoryKey, CategoryName = "Distortion", Description = (string?)null, ExampleCases = (string?)null });
            taxonomyResponse.EnsureSuccessStatusCode();

            var dimensionResponse = await client.PostAsJsonAsync(
                ApiRoutes.AssessmentDimensions.Base.Replace("{examTypeId:guid}", examType.Id.ToString()),
                new CreateAssessmentDimensionRequestBody(
                    FakeGradingLlmClient.DimensionKey,
                    "Meaning transfer",
                    ScaleType.band_1_5,
                    "Band 2 or above",
                    TaskType.A,
                    "{\"1\":\"best\",\"2\":\"ok\",\"3\":\"bad\"}",
                    "2024-02",
                    DateTimeOffset.UtcNow,
                    null,
                    null));
            dimensionResponse.EnsureSuccessStatusCode();

            var questionResponse = await client.PostAsJsonAsync(ApiRoutes.Questions.Base, new
            {
                TaskType = TaskType.A,
                Difficulty = Difficulty.medium,
                Title = "API Test Question",
                Brief = (string?)null,
                SourceText = "Original source text to translate.",
                FlawedTranslationText = (string?)null,
                WordCount = 100,
                CreatedBy = (Guid?)null,
                Visibility = Visibility.Private,
                MeaningCheckpoints = new[] { new { CheckpointText = "Must convey X.", CheckpointType = (string?)null, Importance = CheckpointImportance.core } },
                SeededErrors = Array.Empty<object>(),
            });
            questionResponse.EnsureSuccessStatusCode();
            var question = await questionResponse.Content.ReadFromJsonAsync<ImportUserQuestionResult>();

            var userId = await _factory.SeedUserAsync();

            return (examType.Id, question!.Id, userId);
        }

        // Mirrors AssessmentDimensionsController.CreateAssessmentDimensionRequest — nested inside
        // the controller so it can't be referenced directly (same duplication as SubmissionsControllerTests).
        private record CreateAssessmentDimensionRequestBody(
            string DimensionKey,
            string DimensionName,
            ScaleType ScaleType,
            string? PassThreshold,
            TaskType? ApplicableTaskType,
            string LevelDescriptions,
            string RubricVersion,
            DateTimeOffset EffectiveFrom,
            DateTimeOffset? EffectiveTo,
            string? SourceReference);

        private async Task SeedDeepLearningPromptTemplateAsync()
        {
            // The real add_deep_learning_prompt_template.sql is hand-run against Supabase, not
            // applied to this throwaway Testcontainers DB — seed a minimal marker template
            // directly, same convention as SubmissionsControllerTests' TaskB flawed-text test.
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.PromptTemplates.AddAsync(new PromptTemplate
            {
                Id = Guid.NewGuid(),
                SubjectCategory = SubjectCategory.translation,
                TemplateType = AiOperationType.deep_learning,
                Layer = TemplateLayer.shared_methodology,
                TemplateContent = DeepLearningMarkerTemplate,
                Version = 1,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync();
        }

        [Fact]
        public async Task Generate_is_isolated_from_grading_context_persists_content_and_is_idempotent_on_a_second_call()
        {
            var gradingClient = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddScoped<ILlmClientResolver, FakeGradingLlmClientResolver>()))
                .CreateClient();

            var (examTypeId, questionId, userId) = await SeedExamTypeQuestionAndUserAsync(gradingClient);
            await SeedDeepLearningPromptTemplateAsync();

            // Grade a submission whose content carries a marker that must NEVER leak into a
            // deep-learning prompt for the same question — proving design doc §10.2's isolation
            // the other direction from grading's own isolation from reference_translations.
            var createResponse = await gradingClient.PostAsJsonAsync(ApiRoutes.Submissions.Base, new
            {
                QuestionId = questionId,
                UserId = userId,
                TaskType = TaskType.A,
                Content = $"\"{GradingMarker}\"",
            });
            var submission = await createResponse.Content.ReadFromJsonAsync<CreateSubmissionResult>();
            var gradeResponse = await gradingClient.PostAsJsonAsync(
                $"{ApiRoutes.Submissions.Base}/{submission!.Id}/grade", new { ExamTypeId = examTypeId });
            Assert.Equal(HttpStatusCode.OK, gradeResponse.StatusCode);

            var fakeClient = new FakeDeepLearningLlmClient();
            var deepLearningClient = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddSingleton<ILlmClientResolver>(new FixedLlmClientResolver(fakeClient))))
                .CreateClient();

            var firstGenerate = await deepLearningClient.PostAsJsonAsync(
                $"{ApiRoutes.Questions.Base}/{questionId}/deep-learning", new { ExamTypeId = examTypeId });
            Assert.Equal(HttpStatusCode.OK, firstGenerate.StatusCode);
            var firstResult = await firstGenerate.Content.ReadFromJsonAsync<GenerateDeepLearningContentResult>();

            Assert.False(firstResult!.WasCached);
            Assert.Equal(FakeDeepLearningLlmClient.ReferenceText, firstResult.ReferenceText);
            Assert.Single(firstResult.SentencePatterns);
            Assert.Equal(FakeDeepLearningLlmClient.PatternName, firstResult.SentencePatterns[0].PatternName);
            Assert.Single(firstResult.VocabExpressions);
            Assert.Equal(FakeDeepLearningLlmClient.VocabExpr, firstResult.VocabExpressions[0].EnglishExpr);

            // Isolation: the prompt must carry the source text (proving the template actually
            // rendered) but must NEVER contain anything from the grading call above.
            Assert.Single(fakeClient.CapturedPrompts);
            Assert.Contains("DEEP_LEARNING_SOURCE_MARKER: Original source text to translate.", fakeClient.CapturedPrompts[0]);
            Assert.DoesNotContain(GradingMarker, fakeClient.CapturedPrompts[0]);

            // Idempotency: a second call for the same Question must return the cached row and
            // must NOT reach the LLM client a second time.
            var secondGenerate = await deepLearningClient.PostAsJsonAsync(
                $"{ApiRoutes.Questions.Base}/{questionId}/deep-learning", new { ExamTypeId = examTypeId });
            Assert.Equal(HttpStatusCode.OK, secondGenerate.StatusCode);
            var secondResult = await secondGenerate.Content.ReadFromJsonAsync<GenerateDeepLearningContentResult>();
            Assert.True(secondResult!.WasCached);
            Assert.Equal(1, fakeClient.CallCount);

            var getResponse = await deepLearningClient.GetAsync($"{ApiRoutes.Questions.Base}/{questionId}/deep-learning");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            var fetched = await getResponse.Content.ReadFromJsonAsync<GetDeepLearningContentByQuestionIdResult>();
            Assert.Equal(FakeDeepLearningLlmClient.ReferenceText, fetched!.ReferenceText);
            Assert.Single(fetched.SentencePatterns);
            Assert.Single(fetched.VocabExpressions);
        }

        [Fact]
        public async Task Get_returns_404_before_any_content_has_been_generated_for_the_question()
        {
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddScoped<ILlmClientResolver, FakeGradingLlmClientResolver>()))
                .CreateClient();

            var (_, questionId, _) = await SeedExamTypeQuestionAndUserAsync(client);

            var response = await client.GetAsync($"{ApiRoutes.Questions.Base}/{questionId}/deep-learning");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Generate_rejects_a_response_with_an_empty_pattern_name_instead_of_persisting_it()
        {
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddScoped<ILlmClientResolver, FakeDeepLearningLlmClientResolverWithInvalidPattern>()))
                .CreateClient();

            var (examTypeId, questionId, _) = await SeedExamTypeQuestionAndUserAsync(client);
            await SeedDeepLearningPromptTemplateAsync();

            var response = await client.PostAsJsonAsync(
                $"{ApiRoutes.Questions.Base}/{questionId}/deep-learning", new { ExamTypeId = examTypeId });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

            var getResponse = await client.GetAsync($"{ApiRoutes.Questions.Base}/{questionId}/deep-learning");
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }

        [Fact]
        public async Task Grading_a_new_submission_after_deep_learning_content_exists_marks_its_patterns_and_vocab_as_reviewed()
        {
            var gradingClient = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddScoped<ILlmClientResolver, FakeGradingLlmClientResolver>()))
                .CreateClient();

            var (examTypeId, questionId, userId) = await SeedExamTypeQuestionAndUserAsync(gradingClient);
            await SeedDeepLearningPromptTemplateAsync();

            var deepLearningClient = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddSingleton<ILlmClientResolver>(new FixedLlmClientResolver(new FakeDeepLearningLlmClient()))))
                .CreateClient();
            var generateResponse = await deepLearningClient.PostAsJsonAsync(
                $"{ApiRoutes.Questions.Base}/{questionId}/deep-learning", new { ExamTypeId = examTypeId });
            Assert.Equal(HttpStatusCode.OK, generateResponse.StatusCode);

            var createResponse = await gradingClient.PostAsJsonAsync(ApiRoutes.Submissions.Base, new
            {
                QuestionId = questionId,
                UserId = userId,
                TaskType = TaskType.A,
                Content = "\"my translation of the text\"",
            });
            var submission = await createResponse.Content.ReadFromJsonAsync<CreateSubmissionResult>();
            var gradeResponse = await gradingClient.PostAsJsonAsync(
                $"{ApiRoutes.Submissions.Base}/{submission!.Id}/grade", new { ExamTypeId = examTypeId });
            Assert.Equal(HttpStatusCode.OK, gradeResponse.StatusCode);

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var patternReview = await context.UserPatternReview.SingleAsync(x => x.UserId == userId);
            Assert.Equal(1, patternReview.TimesEncountered);

            var vocabReview = await context.UserVocabReview.SingleAsync(x => x.UserId == userId);
            Assert.Equal(1, vocabReview.TimesEncountered);
        }
    }
}
