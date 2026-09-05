using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateExamType;
using DeepLearning.Application.Features.FollowUpThreads;
using DeepLearning.Application.Features.Questions.Commands.ImportUserQuestion;
using DeepLearning.Application.Features.StandardOverrides.Queries.GetStandardOverrideById;
using DeepLearning.Application.Features.Submissions.Commands.CreateSubmission;
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
    /// <summary>
    /// Multi-round follow-up threads (design decision, 2026-09-02) — replaces the retired
    /// single-shot FollowUpsControllerTests. Same test infrastructure conventions:
    /// FakeFollowUpFlowLlmClient tells grading / per-round follow-up / closing-summary calls
    /// apart by a literal marker each seeded PromptTemplate row renders verbatim (the real
    /// content-injection templates are hand-run against Supabase, not applied to this throwaway
    /// Testcontainers DB); dimensionKey is interpolated per test so tests don't pollute each
    /// other's standard_overrides confirmation counts through the shared DB.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class FollowUpThreadsControllerTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public FollowUpThreadsControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private const string PerRoundResponseJson = """
            { "aiResponse": "Here is my take on this round.", "verdict": "partial" }
            """;

        private const string SummaryUserIncorrectJson = """
            { "aiResponse": "Overall the original grading stands.", "finalVerdict": "user_incorrect", "standardRevision": null }
            """;

        // A thread that only ever asked knowledge questions, never disputed a judgment.
        private const string SummaryNoDisputeJson = """
            { "aiResponse": "This thread was just Q&A about phrasing.", "finalVerdict": null, "standardRevision": null }
            """;

        private static string SummaryUserCorrectJson(string dimensionKey) => $$"""
            {
              "aiResponse": "Overall you're right, the rubric was misapplied.",
              "finalVerdict": "user_correct",
              "standardRevision": {"scope": "grading_rubric", "dimensionOrRule": "{{dimensionKey}}", "originalRuleText": "old", "revisedRuleText": "new"}
            }
            """;

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

        private static async Task<Guid> SeedExamTypeWithDimensionAsync(HttpClient client, string dimensionKey)
        {
            var examTypeResponse = await client.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, new
            {
                Code = $"test_{Guid.NewGuid():N}",
                Name = "API Test Exam Type",
                SubjectCategory = SubjectCategory.translation,
            });
            examTypeResponse.EnsureSuccessStatusCode();
            var examType = await examTypeResponse.Content.ReadFromJsonAsync<CreateExamTypeResult>();

            var dimensionResponse = await client.PostAsJsonAsync(
                ApiRoutes.AssessmentDimensions.Base.Replace("{examTypeId:guid}", examType!.Id.ToString()),
                new CreateAssessmentDimensionRequestBody(
                    dimensionKey,
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

            return examType.Id;
        }

        private static async Task SeedTemplatesAsync(ApiWebApplicationFactory factory)
        {
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.PromptTemplates.AddRangeAsync(
                NewTemplate(AiOperationType.grading, FakeFollowUpFlowLlmClient.GradingMarker),
                NewTemplate(AiOperationType.followup, FakeFollowUpFlowLlmClient.FollowUpMarker),
                NewTemplate(AiOperationType.followup_summary, FakeFollowUpFlowLlmClient.SummaryMarker));
            await context.SaveChangesAsync();

            static PromptTemplate NewTemplate(AiOperationType type, string marker) => new()
            {
                Id = Guid.NewGuid(),
                SubjectCategory = SubjectCategory.translation,
                TemplateType = type,
                Layer = TemplateLayer.shared_methodology,
                TemplateContent = marker,
                Version = 1,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };
        }

        private async Task<(Guid QuestionId, Guid UserId, Guid SubmissionId)> SeedSubmittedSubmissionAsync(HttpClient client)
        {
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
                MeaningCheckpoints = Array.Empty<object>(),
                SeededErrors = Array.Empty<object>(),
            });
            questionResponse.EnsureSuccessStatusCode();
            var question = await questionResponse.Content.ReadFromJsonAsync<ImportUserQuestionResult>();

            var userId = await _factory.SeedUserAsync();

            var createResponse = await client.PostAsJsonAsync(ApiRoutes.Submissions.Base, new
            {
                QuestionId = question!.Id,
                UserId = userId,
                TaskType = TaskType.A,
                Content = "\"my translation of the text\"",
            });
            createResponse.EnsureSuccessStatusCode();
            var submission = await createResponse.Content.ReadFromJsonAsync<CreateSubmissionResult>();

            return (question.Id, userId, submission!.Id);
        }

        private async Task<(Guid QuestionId, Guid UserId, Guid SubmissionId)> SeedGradedSubmissionAsync(HttpClient client, Guid examTypeId)
        {
            var seeded = await SeedSubmittedSubmissionAsync(client);
            var gradeResponse = await client.PostAsJsonAsync($"{ApiRoutes.Submissions.Base}/{seeded.SubmissionId}/grade", new { ExamTypeId = examTypeId });
            gradeResponse.EnsureSuccessStatusCode();
            return seeded;
        }

        private HttpClient CreateClient(string dimensionKey, string? summaryResponseJson = null) => _factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddSingleton<ILlmClientResolver>(
                    LlmClientResolverSubstitute.Returning(
                        new FakeFollowUpFlowLlmClient(dimensionKey, PerRoundResponseJson, summaryResponseJson)))))
            .CreateClient();

        private static Task<HttpResponseMessage> CreateThreadAsync(HttpClient client, Guid submissionId, Guid userId, Guid examTypeId, string questionText)
            => client.PostAsJsonAsync(ApiRoutes.FollowUpThreads.Base, new
            {
                SubmissionId = submissionId,
                UserId = userId,
                ExamTypeId = examTypeId,
                ContextRef = (string?)null,
                QuestionText = questionText,
            });

        [Fact]
        public async Task Create_starts_a_thread_holds_the_submission_under_dispute_and_records_the_first_round()
        {
            var dimensionKey = $"meaning_transfer_{Guid.NewGuid():N}";
            var client = CreateClient(dimensionKey);
            var examTypeId = await SeedExamTypeWithDimensionAsync(client, dimensionKey);
            await SeedTemplatesAsync(_factory);
            var (_, userId, submissionId) = await SeedGradedSubmissionAsync(client, examTypeId);

            var response = await CreateThreadAsync(client, submissionId, userId, examTypeId, "Why was this marked wrong?");

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var thread = await response.Content.ReadFromJsonAsync<FollowUpThreadResult>();
            Assert.Equal(FollowUpThreadStatus.open, thread!.Status);
            Assert.Equal(SubmissionStatus.under_dispute, thread.SubmissionStatus);
            Assert.Null(thread.FinalVerdict);
            Assert.Collection(thread.Messages,
                m => Assert.Equal(FollowUpMessageRole.user, m.Role),
                m =>
                {
                    Assert.Equal(FollowUpMessageRole.ai, m.Role);
                    Assert.Equal(FollowUpVerdict.partial, m.Verdict);
                });

            var getResponse = await client.GetAsync($"{ApiRoutes.FollowUpThreads.Base}/{thread.Id}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        }

        [Fact]
        public async Task Create_returns_409_when_an_open_thread_already_exists_for_the_submission()
        {
            var dimensionKey = $"meaning_transfer_{Guid.NewGuid():N}";
            var client = CreateClient(dimensionKey);
            var examTypeId = await SeedExamTypeWithDimensionAsync(client, dimensionKey);
            await SeedTemplatesAsync(_factory);
            var (_, userId, submissionId) = await SeedGradedSubmissionAsync(client, examTypeId);

            (await CreateThreadAsync(client, submissionId, userId, examTypeId, "First?")).EnsureSuccessStatusCode();
            var second = await CreateThreadAsync(client, submissionId, userId, examTypeId, "Again?");

            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        }

        [Fact]
        public async Task Create_is_allowed_again_after_the_previous_thread_is_closed()
        {
            var dimensionKey = $"meaning_transfer_{Guid.NewGuid():N}";
            var client = CreateClient(dimensionKey, SummaryUserIncorrectJson);
            var examTypeId = await SeedExamTypeWithDimensionAsync(client, dimensionKey);
            await SeedTemplatesAsync(_factory);
            var (_, userId, submissionId) = await SeedGradedSubmissionAsync(client, examTypeId);

            var first = await (await CreateThreadAsync(client, submissionId, userId, examTypeId, "Dispute one")).Content
                .ReadFromJsonAsync<FollowUpThreadResult>();
            (await client.PostAsJsonAsync($"{ApiRoutes.FollowUpThreads.Base}/{first!.Id}/close", new { UserId = userId }))
                .EnsureSuccessStatusCode();

            var secondResponse = await CreateThreadAsync(client, submissionId, userId, examTypeId, "An unrelated question");
            Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
            var second = await secondResponse.Content.ReadFromJsonAsync<FollowUpThreadResult>();
            Assert.NotEqual(first.Id, second!.Id);
            Assert.Equal(SubmissionStatus.under_dispute, second.SubmissionStatus);

            var listResponse = await client.GetAsync($"{ApiRoutes.FollowUpThreads.Base}?submissionId={submissionId}");
            listResponse.EnsureSuccessStatusCode();
            var list = await listResponse.Content.ReadFromJsonAsync<List<FollowUpThreadSummary>>();
            Assert.Equal(2, list!.Count);
            // Newest first.
            Assert.Equal(second.Id, list[0].Id);
            Assert.Equal(FollowUpThreadStatus.open, list[0].Status);
            Assert.Equal(FollowUpThreadStatus.closed, list[1].Status);
        }

        [Fact]
        public async Task Create_returns_409_when_the_submission_is_not_yet_graded()
        {
            var dimensionKey = $"meaning_transfer_{Guid.NewGuid():N}";
            var client = CreateClient(dimensionKey);
            var examTypeId = await SeedExamTypeWithDimensionAsync(client, dimensionKey);
            await SeedTemplatesAsync(_factory);
            var (_, userId, submissionId) = await SeedSubmittedSubmissionAsync(client);

            var response = await CreateThreadAsync(client, submissionId, userId, examTypeId, "Why?");

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task AddMessage_appends_a_round_and_keeps_the_thread_open()
        {
            var dimensionKey = $"meaning_transfer_{Guid.NewGuid():N}";
            var client = CreateClient(dimensionKey);
            var examTypeId = await SeedExamTypeWithDimensionAsync(client, dimensionKey);
            await SeedTemplatesAsync(_factory);
            var (_, userId, submissionId) = await SeedGradedSubmissionAsync(client, examTypeId);

            var created = await (await CreateThreadAsync(client, submissionId, userId, examTypeId, "Round 1")).Content
                .ReadFromJsonAsync<FollowUpThreadResult>();

            var addResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.FollowUpThreads.Base}/{created!.Id}/messages",
                new { UserId = userId, QuestionText = "A follow-up point." });

            Assert.Equal(HttpStatusCode.OK, addResponse.StatusCode);
            var thread = await addResponse.Content.ReadFromJsonAsync<FollowUpThreadResult>();
            Assert.Equal(FollowUpThreadStatus.open, thread!.Status);
            Assert.Equal(SubmissionStatus.under_dispute, thread.SubmissionStatus);
            Assert.Equal(4, thread.Messages.Count);
            Assert.Equal(FollowUpMessageRole.user, thread.Messages[2].Role);
            Assert.Equal(FollowUpMessageRole.ai, thread.Messages[3].Role);
        }

        [Fact]
        public async Task AddMessage_returns_409_after_the_round_cap()
        {
            var dimensionKey = $"meaning_transfer_{Guid.NewGuid():N}";
            var client = CreateClient(dimensionKey);
            var examTypeId = await SeedExamTypeWithDimensionAsync(client, dimensionKey);
            await SeedTemplatesAsync(_factory);
            var (_, userId, submissionId) = await SeedGradedSubmissionAsync(client, examTypeId);

            var created = await (await CreateThreadAsync(client, submissionId, userId, examTypeId, "Round 1")).Content
                .ReadFromJsonAsync<FollowUpThreadResult>();

            // Round 1 came from Create; add rounds until the cap (FollowUpThread.MaxRounds user messages) is hit.
            HttpResponseMessage? last = null;
            for (var i = 2; i <= FollowUpThread.MaxRounds + 1; i++)
            {
                last = await client.PostAsJsonAsync(
                    $"{ApiRoutes.FollowUpThreads.Base}/{created!.Id}/messages",
                    new { UserId = userId, QuestionText = $"Round {i}" });
                if (i <= FollowUpThread.MaxRounds)
                {
                    last.EnsureSuccessStatusCode();
                }
            }

            Assert.Equal(HttpStatusCode.Conflict, last!.StatusCode);
        }

        [Fact]
        public async Task Close_with_user_incorrect_ends_the_submission_at_graded_and_creates_no_override()
        {
            var dimensionKey = $"meaning_transfer_{Guid.NewGuid():N}";
            var client = CreateClient(dimensionKey, SummaryUserIncorrectJson);
            var examTypeId = await SeedExamTypeWithDimensionAsync(client, dimensionKey);
            await SeedTemplatesAsync(_factory);
            var (_, userId, submissionId) = await SeedGradedSubmissionAsync(client, examTypeId);

            var created = await (await CreateThreadAsync(client, submissionId, userId, examTypeId, "Why?")).Content
                .ReadFromJsonAsync<FollowUpThreadResult>();

            var closeResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.FollowUpThreads.Base}/{created!.Id}/close", new { UserId = userId });

            Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
            var thread = await closeResponse.Content.ReadFromJsonAsync<FollowUpThreadResult>();
            Assert.Equal(FollowUpThreadStatus.closed, thread!.Status);
            Assert.Equal(FollowUpVerdict.user_incorrect, thread.FinalVerdict);
            Assert.Equal(SubmissionStatus.graded, thread.SubmissionStatus);
            Assert.Null(thread.StandardOverrideId);
            Assert.NotNull(thread.ClosedAt);
        }

        [Fact]
        public async Task Close_with_user_correct_creates_an_observing_override_and_ends_the_submission_at_graded()
        {
            var dimensionKey = $"meaning_transfer_{Guid.NewGuid():N}";
            var client = CreateClient(dimensionKey, SummaryUserCorrectJson(dimensionKey));
            var examTypeId = await SeedExamTypeWithDimensionAsync(client, dimensionKey);
            await SeedTemplatesAsync(_factory);
            var (_, userId, submissionId) = await SeedGradedSubmissionAsync(client, examTypeId);

            var created = await (await CreateThreadAsync(client, submissionId, userId, examTypeId, "Why?")).Content
                .ReadFromJsonAsync<FollowUpThreadResult>();

            var closeResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.FollowUpThreads.Base}/{created!.Id}/close", new { UserId = userId });

            Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
            var thread = await closeResponse.Content.ReadFromJsonAsync<FollowUpThreadResult>();
            Assert.Equal(FollowUpVerdict.user_correct, thread!.FinalVerdict);
            // Passes through StandardRevised on the way, but ends back at Graded.
            Assert.Equal(SubmissionStatus.graded, thread.SubmissionStatus);
            Assert.NotNull(thread.StandardOverrideId);
            // One confirmation only — below StandardOverrideActivationPolicy's default threshold of 3.
            Assert.Equal(OverrideStatus.observing, thread.StandardOverrideStatus);

            var overrideResponse = await client.GetAsync($"{ApiRoutes.StandardOverrides.Base}/{thread.StandardOverrideId}");
            Assert.Equal(HttpStatusCode.OK, overrideResponse.StatusCode);
            var overrideResult = await overrideResponse.Content.ReadFromJsonAsync<GetStandardOverrideByIdResult>();
            Assert.Equal(OverrideScope.grading_rubric, overrideResult!.Scope);
        }

        [Fact]
        public async Task Close_returns_409_when_the_thread_is_already_closed()
        {
            var dimensionKey = $"meaning_transfer_{Guid.NewGuid():N}";
            var client = CreateClient(dimensionKey, SummaryUserIncorrectJson);
            var examTypeId = await SeedExamTypeWithDimensionAsync(client, dimensionKey);
            await SeedTemplatesAsync(_factory);
            var (_, userId, submissionId) = await SeedGradedSubmissionAsync(client, examTypeId);

            var created = await (await CreateThreadAsync(client, submissionId, userId, examTypeId, "Why?")).Content
                .ReadFromJsonAsync<FollowUpThreadResult>();

            var firstClose = await client.PostAsJsonAsync(
                $"{ApiRoutes.FollowUpThreads.Base}/{created!.Id}/close", new { UserId = userId });
            firstClose.EnsureSuccessStatusCode();

            var secondClose = await client.PostAsJsonAsync(
                $"{ApiRoutes.FollowUpThreads.Base}/{created.Id}/close", new { UserId = userId });
            Assert.Equal(HttpStatusCode.Conflict, secondClose.StatusCode);
        }

        [Fact]
        public async Task List_returns_an_empty_array_before_any_thread_exists()
        {
            var dimensionKey = $"meaning_transfer_{Guid.NewGuid():N}";
            var client = CreateClient(dimensionKey);
            var examTypeId = await SeedExamTypeWithDimensionAsync(client, dimensionKey);
            await SeedTemplatesAsync(_factory);
            var (_, _, submissionId) = await SeedGradedSubmissionAsync(client, examTypeId);

            var response = await client.GetAsync($"{ApiRoutes.FollowUpThreads.Base}?submissionId={submissionId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var list = await response.Content.ReadFromJsonAsync<List<FollowUpThreadSummary>>();
            Assert.Empty(list!);
        }

        [Fact]
        public async Task Close_with_no_dispute_records_a_null_final_verdict_and_no_override()
        {
            var dimensionKey = $"meaning_transfer_{Guid.NewGuid():N}";
            var client = CreateClient(dimensionKey, SummaryNoDisputeJson);
            var examTypeId = await SeedExamTypeWithDimensionAsync(client, dimensionKey);
            await SeedTemplatesAsync(_factory);
            var (_, userId, submissionId) = await SeedGradedSubmissionAsync(client, examTypeId);

            var created = await (await CreateThreadAsync(client, submissionId, userId, examTypeId, "How is carer usually translated?")).Content
                .ReadFromJsonAsync<FollowUpThreadResult>();

            var closeResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.FollowUpThreads.Base}/{created!.Id}/close", new { UserId = userId });

            Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
            var thread = await closeResponse.Content.ReadFromJsonAsync<FollowUpThreadResult>();
            Assert.Equal(FollowUpThreadStatus.closed, thread!.Status);
            Assert.Null(thread.FinalVerdict);
            Assert.Null(thread.StandardOverrideId);
            Assert.Equal(SubmissionStatus.graded, thread.SubmissionStatus);
        }
    }
}
