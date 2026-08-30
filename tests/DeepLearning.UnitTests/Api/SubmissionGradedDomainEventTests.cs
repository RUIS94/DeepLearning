using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateAssessmentDimension;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateExamType;
using DeepLearning.Application.Features.Questions.Commands.ImportUserQuestion;
using DeepLearning.Application.Features.Submissions.Commands.CreateSubmission;
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
    /// Design doc §11.2's Step 6 test requirement — "发布真实事件,验证所有订阅者被MediatR正确
    /// 触发并各自落库,这是验证'新增下游功能只需要新增一个订阅者'这一设计承诺是否成立的关键测试" —
    /// proven end to end: one real POST .../grade call, through the real UnitOfWork's domain
    /// event dispatch, through all three real SubmissionGradedEvent subscribers
    /// (UpdateWeakPointsOnGraded / UpdateProgressOnGraded / ExtractKnowledgePointsOnGraded), each
    /// asserted by querying its own table afterward. GradeSubmissionCommandHandler itself has no
    /// knowledge any of these three exist.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class SubmissionGradedDomainEventTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public SubmissionGradedDomainEventTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Grading_a_submission_creates_a_weak_point_a_progress_snapshot_and_marks_linked_patterns_and_vocab_as_reviewed()
        {
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddScoped<ILlmClientResolver, FakeGradingLlmClientResolver>()))
                .CreateClient();

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

            // Pre-attach one SentencePattern and one VocabExpression to this Question — Step 7's
            // AI extraction isn't built yet, so this stands in for "a question that already has
            // linked review-library material", the case ExtractKnowledgePointsOnGraded actually
            // acts on.
            Guid patternId, vocabId;
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var pattern = new SentencePattern
                {
                    Id = Guid.NewGuid(),
                    QuestionId = question!.Id,
                    PatternName = "Cleft sentence",
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                var vocab = new VocabExpression
                {
                    Id = Guid.NewGuid(),
                    QuestionId = question.Id,
                    EnglishExpr = "in light of",
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                await context.SentencePatterns.AddAsync(pattern);
                await context.VocabExpressions.AddAsync(vocab);
                await context.SaveChangesAsync();
                patternId = pattern.Id;
                vocabId = vocab.Id;
            }

            var createResponse = await client.PostAsJsonAsync(ApiRoutes.Submissions.Base, new
            {
                QuestionId = question!.Id,
                UserId = userId,
                TaskType = TaskType.A,
                Content = "\"my translation of the text\"",
            });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var submission = await createResponse.Content.ReadFromJsonAsync<CreateSubmissionResult>();

            var gradeResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.Submissions.Base}/{submission!.Id}/grade", new { ExamTypeId = examType.Id });
            Assert.Equal(HttpStatusCode.OK, gradeResponse.StatusCode);

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // UpdateWeakPointsOnGraded
                var weakPoint = await context.WeakPoints.SingleAsync(x => x.UserId == userId);
                Assert.Equal("Meaning transfer - Distortion", weakPoint.Category);
                Assert.Equal(WeakPointStatus.active, weakPoint.Status);
                var occurrence = await context.WeakPointOccurrences.SingleAsync(x => x.SubmissionId == submission.Id);
                Assert.Equal(weakPoint.Id, occurrence.WeakPointId);
                Assert.False(occurrence.IsRecurrence);

                // UpdateProgressOnGraded
                var snapshot = await context.ProgressSnapshots.SingleAsync(x => x.UserId == userId);
                Assert.Equal("medium", snapshot.DifficultyTier);
                Assert.Equal(2.0m, snapshot.AvgBandMeaningTransfer);
                Assert.Equal(100m, snapshot.PassRate);

                // ExtractKnowledgePointsOnGraded
                var patternReview = await context.UserPatternReview.SingleAsync(x => x.UserId == userId && x.PatternId == patternId);
                Assert.Equal(1, patternReview.TimesEncountered);
                var vocabReview = await context.UserVocabReview.SingleAsync(x => x.UserId == userId && x.VocabId == vocabId);
                Assert.Equal(1, vocabReview.TimesEncountered);
            }
        }

        // Mirrors AssessmentDimensionsController.CreateAssessmentDimensionRequest — that record
        // is nested inside the controller so it can't be referenced directly from the test project
        // (same duplication SubmissionsControllerTests already carries for the same reason).
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
    }
}
