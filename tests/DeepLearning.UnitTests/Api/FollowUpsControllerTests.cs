using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateAssessmentDimension;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateExamType;
using DeepLearning.Application.Features.FollowUps.Commands.CreateFollowUpQuestion;
using DeepLearning.Application.Features.Questions.Commands.ImportUserQuestion;
using DeepLearning.Application.Features.StandardOverrides.Commands.ActivateStandardOverride;
using DeepLearning.Application.Features.StandardOverrides.Queries.GetStandardOverrideById;
using DeepLearning.Application.Features.StandardOverrides.Queries.ListStandardOverrides;
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
    /// Design doc §11.2's Step 5 test strategy: "追问接口契约,verdict驱动override状态变化的完整链路".
    /// FakeFollowUpFlowLlmClient tells a grading call apart from a follow-up call by a literal
    /// marker string each test seeds into the corresponding PromptTemplate row (the real
    /// content-injection templates are hand-run against Supabase, not applied to this throwaway
    /// Testcontainers DB — same convention as SubmissionsControllerTests' own TaskB test).
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class FollowUpsControllerTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public FollowUpsControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        // dimensionKey is interpolated per test (not a shared constant) — see
        // FakeFollowUpFlowLlmClient's doc comment for why a shared literal would let tests
        // pollute each other's standard_overrides confirmation counts.
        private static string UserCorrectResponseJson(string dimensionKey) => $$"""
            {
              "aiResponse": "You're right, the rubric was misapplied here.",
              "verdict": "user_correct",
              "standardRevision": {"scope": "grading_rubric", "dimensionOrRule": "{{dimensionKey}}", "originalRuleText": "old rule text", "revisedRuleText": "new rule text"}
            }
            """;

        private const string UserIncorrectResponseJson = """
            {
              "aiResponse": "The original grading was correct.",
              "verdict": "user_incorrect",
              "standardRevision": null
            }
            """;

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

        private static async Task SeedGradingAndFollowUpTemplatesAsync(ApiWebApplicationFactory factory)
        {
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.PromptTemplates.AddRangeAsync(
                new PromptTemplate
                {
                    Id = Guid.NewGuid(),
                    SubjectCategory = SubjectCategory.translation,
                    TemplateType = AiOperationType.grading,
                    Layer = TemplateLayer.shared_methodology,
                    TemplateContent = FakeFollowUpFlowLlmClient.GradingMarker,
                    Version = 1,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                },
                new PromptTemplate
                {
                    Id = Guid.NewGuid(),
                    SubjectCategory = SubjectCategory.translation,
                    TemplateType = AiOperationType.followup,
                    Layer = TemplateLayer.shared_methodology,
                    TemplateContent = FakeFollowUpFlowLlmClient.FollowUpMarker,
                    Version = 1,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                });
            await context.SaveChangesAsync();
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
            var (questionId, userId, submissionId) = await SeedSubmittedSubmissionAsync(client);

            var gradeResponse = await client.PostAsJsonAsync($"{ApiRoutes.Submissions.Base}/{submissionId}/grade", new { ExamTypeId = examTypeId });
            gradeResponse.EnsureSuccessStatusCode();

            return (questionId, userId, submissionId);
        }

        [Fact]
        public async Task Create_transitions_the_submission_back_to_graded_and_creates_no_override_when_verdict_is_user_incorrect()
        {
            var dimensionKey = $"meaning_transfer_{Guid.NewGuid():N}";
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddSingleton<ILlmClientResolver>(new FakeFollowUpFlowLlmClientResolver(dimensionKey, UserIncorrectResponseJson))))
                .CreateClient();

            var examTypeId = await SeedExamTypeWithDimensionAsync(client, dimensionKey);
            await SeedGradingAndFollowUpTemplatesAsync(_factory);
            var (_, userId, submissionId) = await SeedGradedSubmissionAsync(client, examTypeId);

            var response = await client.PostAsJsonAsync(ApiRoutes.FollowUps.Base, new
            {
                SubmissionId = submissionId,
                UserId = userId,
                ExamTypeId = examTypeId,
                ContextRef = (string?)null,
                QuestionText = "Why was this marked wrong?",
            });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<CreateFollowUpQuestionResult>();
            Assert.Equal(FollowUpVerdict.user_incorrect, result!.Verdict);
            Assert.Equal(SubmissionStatus.graded, result.SubmissionStatus);
            Assert.Null(result.StandardOverrideId);

            var getResponse = await client.GetAsync($"{ApiRoutes.FollowUps.Base}/{result.Id}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        }

        [Fact]
        public async Task Create_creates_an_observing_override_and_still_ends_the_submission_at_graded_when_verdict_is_user_correct()
        {
            var dimensionKey = $"meaning_transfer_{Guid.NewGuid():N}";
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddSingleton<ILlmClientResolver>(new FakeFollowUpFlowLlmClientResolver(dimensionKey, UserCorrectResponseJson(dimensionKey)))))
                .CreateClient();

            var examTypeId = await SeedExamTypeWithDimensionAsync(client, dimensionKey);
            await SeedGradingAndFollowUpTemplatesAsync(_factory);
            var (_, userId, submissionId) = await SeedGradedSubmissionAsync(client, examTypeId);

            var response = await client.PostAsJsonAsync(ApiRoutes.FollowUps.Base, new
            {
                SubmissionId = submissionId,
                UserId = userId,
                ExamTypeId = examTypeId,
                ContextRef = (string?)null,
                QuestionText = "Why was this marked wrong?",
            });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<CreateFollowUpQuestionResult>();
            Assert.Equal(FollowUpVerdict.user_correct, result!.Verdict);
            // Passes through StandardRevised on the way, but ends back at Graded either way
            // (design doc §4.1: StandardRevised -> Graded is the very next legal step).
            Assert.Equal(SubmissionStatus.graded, result.SubmissionStatus);
            Assert.NotNull(result.StandardOverrideId);
            // Only one confirmation so far — StandardOverrideActivationPolicy's default
            // threshold (3) isn't met yet, so it stays observing rather than jumping straight to active.
            Assert.Equal(OverrideStatus.observing, result.StandardOverrideStatus);

            var overrideResponse = await client.GetAsync($"{ApiRoutes.StandardOverrides.Base}/{result.StandardOverrideId}");
            Assert.Equal(HttpStatusCode.OK, overrideResponse.StatusCode);
        }

        [Fact]
        public async Task Three_independent_confirmations_on_different_questions_auto_activate_the_override()
        {
            var dimensionKey = $"meaning_transfer_{Guid.NewGuid():N}";
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddSingleton<ILlmClientResolver>(new FakeFollowUpFlowLlmClientResolver(dimensionKey, UserCorrectResponseJson(dimensionKey)))))
                .CreateClient();

            var examTypeId = await SeedExamTypeWithDimensionAsync(client, dimensionKey);
            await SeedGradingAndFollowUpTemplatesAsync(_factory);

            Guid? lastOverrideId = null;
            OverrideStatus? lastStatus = null;
            for (var i = 0; i < 3; i++)
            {
                var (_, userId, submissionId) = await SeedGradedSubmissionAsync(client, examTypeId);

                var response = await client.PostAsJsonAsync(ApiRoutes.FollowUps.Base, new
                {
                    SubmissionId = submissionId,
                    UserId = userId,
                    ExamTypeId = examTypeId,
                    ContextRef = (string?)null,
                    QuestionText = "Why was this marked wrong?",
                });
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);

                var result = await response.Content.ReadFromJsonAsync<CreateFollowUpQuestionResult>();
                lastOverrideId = result!.StandardOverrideId;
                lastStatus = result.StandardOverrideStatus;
            }

            // The 3rd independent confirmation (design doc §10.6's own "如3次" example) crosses
            // StandardOverrideActivationPolicy.DefaultConfirmationsRequired — no manual
            // ActivateStandardOverride call needed for this path.
            Assert.Equal(OverrideStatus.active, lastStatus);

            var listResponse = await client.GetAsync($"{ApiRoutes.StandardOverrides.Base}?status={OverrideStatus.active}");
            listResponse.EnsureSuccessStatusCode();
            var list = await listResponse.Content.ReadFromJsonAsync<List<StandardOverrideResultItem>>();
            Assert.Contains(list!, x => x.Id == lastOverrideId);
        }

        [Fact]
        public async Task Create_returns_409_when_the_submission_is_not_yet_graded()
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

            var (_, userId, submissionId) = await SeedSubmittedSubmissionAsync(client);

            var response = await client.PostAsJsonAsync(ApiRoutes.FollowUps.Base, new
            {
                SubmissionId = submissionId,
                UserId = userId,
                ExamTypeId = examType!.Id,
                ContextRef = (string?)null,
                QuestionText = "Why was this marked wrong?",
            });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task Create_includes_the_reference_translation_in_the_prompt_and_accepts_a_translation_reference_scoped_revision()
        {
            // Design doc §2.1 node W ("对参考译文有疑问") reuses this same follow-up endpoint —
            // this proves the reference translation's own text actually reaches the AI prompt
            // (not just that the code compiles) and that a translation_reference-scoped
            // standardRevision (already a legal OverrideScope value, but never exercised by
            // Step 5's own tests, which only ever used grading_rubric) round-trips correctly.
            var dimensionKey = $"meaning_transfer_{Guid.NewGuid():N}";
            var responseJson = """
                {
                  "aiResponse": "You're right, that phrasing in the reference translation is a bit stiff.",
                  "verdict": "user_correct",
                  "standardRevision": {"scope": "translation_reference", "dimensionOrRule": "reference_wording", "originalRuleText": "used a literal calque", "revisedRuleText": "prefer a more natural phrasing next time"}
                }
                """;
            var fakeClient = new FakeFollowUpFlowLlmClient(dimensionKey, responseJson);
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddSingleton<ILlmClientResolver>(new FixedLlmClientResolver(fakeClient))))
                .CreateClient();

            var examTypeId = await SeedExamTypeWithDimensionAsync(client, dimensionKey);

            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await context.PromptTemplates.AddRangeAsync(
                    new PromptTemplate
                    {
                        Id = Guid.NewGuid(),
                        SubjectCategory = SubjectCategory.translation,
                        TemplateType = AiOperationType.grading,
                        Layer = TemplateLayer.shared_methodology,
                        TemplateContent = FakeFollowUpFlowLlmClient.GradingMarker,
                        Version = 1,
                        IsActive = true,
                        CreatedAt = DateTimeOffset.UtcNow,
                    },
                    new PromptTemplate
                    {
                        Id = Guid.NewGuid(),
                        SubjectCategory = SubjectCategory.translation,
                        TemplateType = AiOperationType.followup,
                        Layer = TemplateLayer.shared_methodology,
                        // The real {{ if reference_translation }} guard, exercised against the
                        // handler's real BuildTemplateModel output rather than the production
                        // add_followup_reference_translation_content.sql wording (that file is
                        // hand-run against Supabase, not applied to this throwaway Testcontainers
                        // DB — same convention as every other content-injection test in this file).
                        TemplateContent = $"{FakeFollowUpFlowLlmClient.FollowUpMarker}\n{{{{ if reference_translation }}}}REF: {{{{ reference_translation.reference_text }}}}{{{{ end }}}}",
                        Version = 1,
                        IsActive = true,
                        CreatedAt = DateTimeOffset.UtcNow,
                    });
                await context.SaveChangesAsync();
            }

            var (questionId, userId, submissionId) = await SeedGradedSubmissionAsync(client, examTypeId);

            const string referenceText = "REFERENCE_TRANSLATION_MARKER_abc123";
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await context.ReferenceTranslations.AddAsync(new ReferenceTranslation
                {
                    Id = Guid.NewGuid(),
                    QuestionId = questionId,
                    ReferenceText = referenceText,
                    CreatedAt = DateTimeOffset.UtcNow,
                });
                await context.SaveChangesAsync();
            }

            var response = await client.PostAsJsonAsync(ApiRoutes.FollowUps.Base, new
            {
                SubmissionId = submissionId,
                UserId = userId,
                ExamTypeId = examTypeId,
                ContextRef = (string?)null,
                QuestionText = "Why does the reference translation phrase it that way?",
            });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<CreateFollowUpQuestionResult>();
            Assert.Equal(FollowUpVerdict.user_correct, result!.Verdict);
            Assert.NotNull(result.StandardOverrideId);

            Assert.NotNull(fakeClient.LastFollowUpPrompt);
            Assert.Contains(referenceText, fakeClient.LastFollowUpPrompt);

            var overrideResponse = await client.GetAsync($"{ApiRoutes.StandardOverrides.Base}/{result.StandardOverrideId}");
            Assert.Equal(HttpStatusCode.OK, overrideResponse.StatusCode);
            var overrideResult = await overrideResponse.Content.ReadFromJsonAsync<GetStandardOverrideByIdResult>();
            Assert.Equal(OverrideScope.translation_reference, overrideResult!.Scope);
        }

        [Fact]
        public async Task ActivateStandardOverride_promotes_an_observing_row_regardless_of_the_confirmation_count()
        {
            var dimensionKey = $"meaning_transfer_{Guid.NewGuid():N}";
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddSingleton<ILlmClientResolver>(new FakeFollowUpFlowLlmClientResolver(dimensionKey, UserCorrectResponseJson(dimensionKey)))))
                .CreateClient();

            var examTypeId = await SeedExamTypeWithDimensionAsync(client, dimensionKey);
            await SeedGradingAndFollowUpTemplatesAsync(_factory);
            var (_, userId, submissionId) = await SeedGradedSubmissionAsync(client, examTypeId);

            var createResponse = await client.PostAsJsonAsync(ApiRoutes.FollowUps.Base, new
            {
                SubmissionId = submissionId,
                UserId = userId,
                ExamTypeId = examTypeId,
                ContextRef = (string?)null,
                QuestionText = "Why was this marked wrong?",
            });
            var created = await createResponse.Content.ReadFromJsonAsync<CreateFollowUpQuestionResult>();
            Assert.Equal(OverrideStatus.observing, created!.StandardOverrideStatus);

            // Design doc §10.6's "或经过一次人工复核" path — promotes it despite only one
            // confirmation, well short of the default threshold of 3.
            var activateResponse = await client.PostAsync($"{ApiRoutes.StandardOverrides.Base}/{created.StandardOverrideId}/activate", null);
            Assert.Equal(HttpStatusCode.OK, activateResponse.StatusCode);
            var activated = await activateResponse.Content.ReadFromJsonAsync<ActivateStandardOverrideResult>();
            Assert.Equal(OverrideStatus.active, activated!.Status);

            var secondActivate = await client.PostAsync($"{ApiRoutes.StandardOverrides.Base}/{created.StandardOverrideId}/activate", null);
            Assert.Equal(HttpStatusCode.Conflict, secondActivate.StatusCode);
        }
    }
}
