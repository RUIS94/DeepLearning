using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateAssessmentDimension;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateErrorTaxonomy;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateExamType;
using DeepLearning.Application.Features.Questions.Commands.ImportUserQuestion;
using DeepLearning.Application.Features.Submissions.Commands.CreateSubmission;
using DeepLearning.Application.Features.Submissions.Commands.GradeSubmission;
using DeepLearning.Application.Features.Submissions.Queries.GetSubmissionById;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Persistence;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace DeepLearning.UnitTests.Api
{
    [Collection(ApiCollection.Name)]
    public class SubmissionsControllerTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public SubmissionsControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

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

        private const string TaskBFlawedText = "This sentence has an error in it.";

        private async Task<(Guid ExamTypeId, Guid QuestionId, Guid UserId)> SeedTaskBExamTypeQuestionAndUserAsync(HttpClient client)
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
            var taxonomy = await taxonomyResponse.Content.ReadFromJsonAsync<CreateErrorTaxonomyResult>();

            var dimensionResponse = await client.PostAsJsonAsync(
                ApiRoutes.AssessmentDimensions.Base.Replace("{examTypeId:guid}", examType.Id.ToString()),
                new CreateAssessmentDimensionRequestBody(
                    FakeGradingLlmClient.DimensionKey,
                    "Revision skills",
                    ScaleType.band_1_5,
                    "Band 2 or above",
                    TaskType.B,
                    "{\"1\":\"best\",\"2\":\"ok\",\"3\":\"bad\"}",
                    "2024-02",
                    DateTimeOffset.UtcNow,
                    null,
                    null));
            dimensionResponse.EnsureSuccessStatusCode();

            var questionResponse = await client.PostAsJsonAsync(ApiRoutes.Questions.Base, new
            {
                TaskType = TaskType.B,
                Difficulty = Difficulty.medium,
                Title = "API Test TaskB Question",
                Brief = (string?)null,
                SourceText = "Original source text.",
                FlawedTranslationText = TaskBFlawedText,
                WordCount = 100,
                CreatedBy = (Guid?)null,
                Visibility = Visibility.Private,
                MeaningCheckpoints = Array.Empty<object>(),
                SeededErrors = new[] { new { PositionStart = 9, PositionEnd = 17, ErrorTaxonomyId = taxonomy!.Id, CorrectReferenceText = "had", Note = (string?)null } },
            });
            questionResponse.EnsureSuccessStatusCode();
            var question = await questionResponse.Content.ReadFromJsonAsync<ImportUserQuestionResult>();

            var userId = await _factory.SeedUserAsync();

            return (examType.Id, question!.Id, userId);
        }

        // Mirrors AssessmentDimensionsController.CreateAssessmentDimensionRequest — that record
        // is nested inside the controller so it can't be referenced directly from the test project.
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

        [Fact]
        public async Task Grade_persists_grading_results_and_error_list_and_transitions_the_submission_to_graded()
        {
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddScoped<ILlmClientResolver, FakeGradingLlmClientResolver>()))
                .CreateClient();

            var (examTypeId, questionId, userId) = await SeedExamTypeQuestionAndUserAsync(client);

            var createResponse = await client.PostAsJsonAsync(ApiRoutes.Submissions.Base, new
            {
                QuestionId = questionId,
                UserId = userId,
                TaskType = TaskType.A,
                Content = "\"my translation of the text\"",
            });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var submission = await createResponse.Content.ReadFromJsonAsync<CreateSubmissionResult>();
            Assert.Equal(SubmissionStatus.submitted, submission!.Status);

            var gradeResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.Submissions.Base}/{submission.Id}/grade", new { ExamTypeId = examTypeId });
            Assert.Equal(HttpStatusCode.OK, gradeResponse.StatusCode);
            var graded = await gradeResponse.Content.ReadFromJsonAsync<GradeSubmissionResult>();
            Assert.Equal(SubmissionStatus.graded, graded!.Status);
            Assert.Equal(1, graded.GradingResultCount);
            Assert.Equal(1, graded.ErrorListCount);

            var getResponse = await client.GetAsync($"{ApiRoutes.Submissions.Base}/{submission.Id}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            var fetched = await getResponse.Content.ReadFromJsonAsync<GetSubmissionByIdResult>();

            Assert.Equal(SubmissionStatus.graded, fetched!.Status);
            Assert.Single(fetched.GradingResults);
            Assert.Equal(FakeGradingLlmClient.DimensionKey, fetched.GradingResults[0].DimensionKey);
            Assert.Equal(2, fetched.GradingResults[0].Band);
            Assert.True(fetched.GradingResults[0].PassBool);
            Assert.Single(fetched.ErrorList);
            Assert.Equal(FakeGradingLlmClient.ErrorCategoryKey, fetched.ErrorList[0].ErrorCategory);
            Assert.Equal(ErrorSeverity.moderate, fetched.ErrorList[0].Severity);
            Assert.Equal("fake summary", fetched.ErrorList[0].Summary);
        }

        [Fact]
        public async Task Grading_twice_returns_409_because_the_submission_is_already_graded()
        {
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddScoped<ILlmClientResolver, FakeGradingLlmClientResolver>()))
                .CreateClient();

            var (examTypeId, questionId, userId) = await SeedExamTypeQuestionAndUserAsync(client);

            var createResponse = await client.PostAsJsonAsync(ApiRoutes.Submissions.Base, new
            {
                QuestionId = questionId,
                UserId = userId,
                TaskType = TaskType.A,
                Content = "\"my translation of the text\"",
            });
            var submission = await createResponse.Content.ReadFromJsonAsync<CreateSubmissionResult>();

            var firstGrade = await client.PostAsJsonAsync($"{ApiRoutes.Submissions.Base}/{submission!.Id}/grade", new { ExamTypeId = examTypeId });
            Assert.Equal(HttpStatusCode.OK, firstGrade.StatusCode);

            var secondGrade = await client.PostAsJsonAsync($"{ApiRoutes.Submissions.Base}/{submission.Id}/grade", new { ExamTypeId = examTypeId });
            Assert.Equal(HttpStatusCode.Conflict, secondGrade.StatusCode);
        }

        [Fact]
        public async Task Grade_rejects_an_ai_response_with_an_unknown_error_category_and_marks_the_submission_grading_failed()
        {
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddScoped<ILlmClientResolver, FakeGradingLlmClientResolverWithInvalidCategory>()))
                .CreateClient();

            var (examTypeId, questionId, userId) = await SeedExamTypeQuestionAndUserAsync(client);

            var createResponse = await client.PostAsJsonAsync(ApiRoutes.Submissions.Base, new
            {
                QuestionId = questionId,
                UserId = userId,
                TaskType = TaskType.A,
                Content = "\"my translation of the text\"",
            });
            var submission = await createResponse.Content.ReadFromJsonAsync<CreateSubmissionResult>();

            var gradeResponse = await client.PostAsJsonAsync($"{ApiRoutes.Submissions.Base}/{submission!.Id}/grade", new { ExamTypeId = examTypeId });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, gradeResponse.StatusCode);

            var getResponse = await client.GetAsync($"{ApiRoutes.Submissions.Base}/{submission.Id}");
            var fetched = await getResponse.Content.ReadFromJsonAsync<GetSubmissionByIdResult>();
            Assert.Equal(SubmissionStatus.grading_failed, fetched!.Status);
            Assert.Empty(fetched.GradingResults);
        }

        [Fact]
        public async Task Grade_rejects_an_out_of_range_band_and_marks_the_submission_grading_failed_instead_of_getting_stuck()
        {
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddScoped<ILlmClientResolver, FakeGradingLlmClientResolverWithOutOfRangeBand>()))
                .CreateClient();

            var (examTypeId, questionId, userId) = await SeedExamTypeQuestionAndUserAsync(client);

            var createResponse = await client.PostAsJsonAsync(ApiRoutes.Submissions.Base, new
            {
                QuestionId = questionId,
                UserId = userId,
                TaskType = TaskType.A,
                Content = "\"my translation of the text\"",
            });
            var submission = await createResponse.Content.ReadFromJsonAsync<CreateSubmissionResult>();

            var gradeResponse = await client.PostAsJsonAsync($"{ApiRoutes.Submissions.Base}/{submission!.Id}/grade", new { ExamTypeId = examTypeId });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, gradeResponse.StatusCode);

            var getResponse = await client.GetAsync($"{ApiRoutes.Submissions.Base}/{submission.Id}");
            var fetched = await getResponse.Content.ReadFromJsonAsync<GetSubmissionByIdResult>();
            // GradingFailed, not stuck in Grading — proves the submission can still be retried
            // (Grading is a dead end with no legal transition back out of it except GradingFailed).
            Assert.Equal(SubmissionStatus.grading_failed, fetched!.Status);
            Assert.Empty(fetched.GradingResults);
        }

        [Fact]
        public async Task Grade_for_task_b_includes_the_flawed_translation_text_in_the_prompt_sent_to_the_ai()
        {
            var capturingClient = new CapturingGradingLlmClient();
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddSingleton<ILlmClientResolver>(new FixedLlmClientResolver(capturingClient))))
                .CreateClient();

            var (examTypeId, questionId, userId) = await SeedTaskBExamTypeQuestionAndUserAsync(client);

            // The real content-injection template (add_grading_content_prompt_template.sql) is
            // hand-run against Supabase, not applied to this throwaway Testcontainers DB — seed a
            // minimal stand-in directly so BuildPromptAsync has something to render
            // flawed_translation_text into, decoupled from the production template's wording.
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await context.PromptTemplates.AddAsync(new PromptTemplate
                {
                    Id = Guid.NewGuid(),
                    SubjectCategory = SubjectCategory.translation,
                    TemplateType = AiOperationType.grading,
                    Layer = TemplateLayer.shared_methodology,
                    TemplateContent = "FLAWED TEXT MARKER: {{ flawed_translation_text }}",
                    Version = 1,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                });
                await context.SaveChangesAsync();
            }

            var createResponse = await client.PostAsJsonAsync(ApiRoutes.Submissions.Base, new
            {
                QuestionId = questionId,
                UserId = userId,
                TaskType = TaskType.B,
                Content = "[{\"positionStart\":9,\"positionEnd\":17,\"errorCategory\":\"distortion\",\"correctedText\":\"had\"}]",
            });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var submission = await createResponse.Content.ReadFromJsonAsync<CreateSubmissionResult>();

            var gradeResponse = await client.PostAsJsonAsync($"{ApiRoutes.Submissions.Base}/{submission!.Id}/grade", new { ExamTypeId = examTypeId });
            Assert.Equal(HttpStatusCode.OK, gradeResponse.StatusCode);

            Assert.Contains($"FLAWED TEXT MARKER: {TaskBFlawedText}", capturingClient.CapturedPrompt);
        }

        [Fact]
        public async Task Create_returns_400_when_task_type_does_not_match_the_question()
        {
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddScoped<ILlmClientResolver, FakeGradingLlmClientResolver>()))
                .CreateClient();

            var (_, questionId, userId) = await SeedExamTypeQuestionAndUserAsync(client);

            var response = await client.PostAsJsonAsync(ApiRoutes.Submissions.Base, new
            {
                QuestionId = questionId,
                UserId = userId,
                TaskType = TaskType.B,
                Content = "[]",
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
