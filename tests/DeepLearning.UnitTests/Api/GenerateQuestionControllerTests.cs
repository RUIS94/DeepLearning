using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateErrorTaxonomy;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateExamType;
using DeepLearning.Application.Features.Questions.Commands.GenerateQuestion;
using DeepLearning.Application.Features.Questions.Commands.ImportUserQuestion;
using DeepLearning.Application.Features.Questions.Queries.GetQuestionById;
using DeepLearning.Application.Features.Questions.Queries.GetSeedReferenceLinksByQuestionId;
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

        /// <summary>
        /// Design doc §11.2 Step 8: "额外验证seed_reference_links确实记录了每次出题参考了哪些真题
        /// (可追溯性测试)". Few-shot samples are opt-in now, so this passes an explicit
        /// SeedQuestionIds and asserts the traceability endpoint reports exactly that seed — and
        /// that a second seed row NOT in the list is not linked — proving
        /// GenerateQuestionCommandHandler's SeedReferenceLink persistence is wired end to end.
        /// </summary>
        [Fact]
        public async Task Generate_records_which_seed_reference_questions_were_used_and_they_are_traceable_via_the_api()
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

            Question matchingSeed;
            Question nonMatchingSeed;
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                matchingSeed = new Question
                {
                    Id = Guid.NewGuid(),
                    TaskType = TaskType.A,
                    Difficulty = Difficulty.medium,
                    Title = $"real_exam_seed_{Guid.NewGuid():N}",
                    SourceText = "A real-exam-shaped passage used as few-shot reference.",
                    Origin = QuestionOrigin.real_exam_seed,
                    SourceType = SourceType.real_exam,
                    IsSeedReference = true,
                    Visibility = Visibility.Private,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                nonMatchingSeed = new Question
                {
                    Id = Guid.NewGuid(),
                    TaskType = TaskType.A,
                    Difficulty = Difficulty.hard, // not in SeedQuestionIds — must not be referenced
                    Title = $"real_exam_seed_{Guid.NewGuid():N}",
                    SourceText = "A different real-exam passage, deliberately left out of SeedQuestionIds.",
                    Origin = QuestionOrigin.real_exam_seed,
                    SourceType = SourceType.real_exam,
                    IsSeedReference = true,
                    Visibility = Visibility.Private,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                await context.Questions.AddRangeAsync(matchingSeed, nonMatchingSeed);
                await context.SaveChangesAsync();
            }

            var generateResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.Questions.Base}/generate",
                new
                {
                    ExamTypeId = examType!.Id,
                    TaskType = TaskType.A,
                    Difficulty = Difficulty.medium,
                    SeedQuestionIds = new[] { matchingSeed.Id },
                    CreatedBy = (Guid?)null,
                });
            Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);
            var generated = await generateResponse.Content.ReadFromJsonAsync<GenerateQuestionResult>();

            var linksResponse = await client.GetAsync($"{ApiRoutes.Questions.Base}/{generated!.Id}/seed-references");
            Assert.Equal(HttpStatusCode.OK, linksResponse.StatusCode);
            var links = await linksResponse.Content.ReadFromJsonAsync<List<SeedReferenceLinkResultItem>>();

            var link = Assert.Single(links!);
            Assert.Equal(matchingSeed.Id, link.SeedQuestionId);
            Assert.DoesNotContain(links!, l => l.SeedQuestionId == nonMatchingSeed.Id);
        }

        /// <summary>
        /// User-requested: a caller must be able to hand-pick specific real-exam questions as
        /// generation reference instead of relying on the automatic task-type/difficulty/category
        /// filter. Deliberately seeds a question whose difficulty/task type would NOT match the
        /// generation request (so it could never surface via ListSeedReferenceCandidatesAsync) —
        /// proves SeedQuestionIds really bypasses the automatic filter rather than just narrowing it,
        /// and that the resulting SeedReferenceLink is tagged as a manual pick.
        /// </summary>
        [Fact]
        public async Task Generate_uses_manually_specified_seed_question_ids_and_bypasses_automatic_filtering()
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

            Question manuallyPickedSeed;
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                manuallyPickedSeed = new Question
                {
                    Id = Guid.NewGuid(),
                    TaskType = TaskType.B, // deliberately wrong task type for the generate call below
                    Difficulty = Difficulty.hard, // deliberately wrong difficulty
                    Title = $"real_exam_seed_{Guid.NewGuid():N}",
                    SourceText = "A hand-picked real-exam passage.",
                    Origin = QuestionOrigin.real_exam_seed,
                    SourceType = SourceType.real_exam,
                    IsSeedReference = true,
                    Visibility = Visibility.Private,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                await context.Questions.AddAsync(manuallyPickedSeed);
                await context.SaveChangesAsync();
            }

            var generateResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.Questions.Base}/generate",
                new
                {
                    ExamTypeId = examType!.Id,
                    TaskType = TaskType.A,
                    Difficulty = Difficulty.medium,
                    SeedQuestionIds = new[] { manuallyPickedSeed.Id },
                    CreatedBy = (Guid?)null,
                });
            Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);
            var generated = await generateResponse.Content.ReadFromJsonAsync<GenerateQuestionResult>();

            var linksResponse = await client.GetAsync($"{ApiRoutes.Questions.Base}/{generated!.Id}/seed-references");
            var links = await linksResponse.Content.ReadFromJsonAsync<List<SeedReferenceLinkResultItem>>();

            var link = Assert.Single(links!);
            Assert.Equal(manuallyPickedSeed.Id, link.SeedQuestionId);
            Assert.Equal("manually specified by caller", link.SimilarityReason);
        }

        [Fact]
        public async Task Generate_returns_404_when_a_manually_specified_seed_question_id_does_not_exist()
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
                new
                {
                    ExamTypeId = examType!.Id,
                    TaskType = TaskType.A,
                    Difficulty = Difficulty.medium,
                    SeedQuestionIds = new[] { Guid.NewGuid() },
                    CreatedBy = (Guid?)null,
                });

            Assert.Equal(HttpStatusCode.NotFound, generateResponse.StatusCode);
        }

        [Fact]
        public async Task Generate_returns_400_when_a_manually_specified_seed_question_id_is_not_a_seed_reference()
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

            Question notASeed;
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                notASeed = new Question
                {
                    Id = Guid.NewGuid(),
                    TaskType = TaskType.A,
                    Difficulty = Difficulty.medium,
                    Title = $"not_a_seed_{Guid.NewGuid():N}",
                    SourceText = "An ordinary, non-seed question.",
                    Origin = QuestionOrigin.user_uploaded,
                    SourceType = SourceType.user_generated,
                    IsSeedReference = false,
                    Visibility = Visibility.Private,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                await context.Questions.AddAsync(notASeed);
                await context.SaveChangesAsync();
            }

            var generateResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.Questions.Base}/generate",
                new
                {
                    ExamTypeId = examType!.Id,
                    TaskType = TaskType.A,
                    Difficulty = Difficulty.medium,
                    SeedQuestionIds = new[] { notASeed.Id },
                    CreatedBy = (Guid?)null,
                });

            Assert.Equal(HttpStatusCode.BadRequest, generateResponse.StatusCode);
        }

        /// <summary>
        /// Closes the loop end to end through the API only (no direct DbContext seeding) —
        /// proves a real deployment can populate and use the seed-reference pool without hand-run
        /// SQL: import a question via POST /questions with IsSeedReference=true, then generate a
        /// question that references it via an explicit SeedQuestionIds, and confirm the link was
        /// persisted (few-shot samples are opt-in — the handler no longer auto-selects).
        /// </summary>
        [Fact]
        public async Task A_seed_reference_question_imported_through_the_api_can_be_referenced_by_a_later_generate_call()
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

            var importResponse = await client.PostAsJsonAsync(ApiRoutes.Questions.Base, new
            {
                TaskType = TaskType.A,
                Difficulty = Difficulty.medium,
                Title = $"real_exam_seed_{Guid.NewGuid():N}",
                Brief = (string?)null,
                SourceText = "A real-exam passage imported through the API.",
                FlawedTranslationText = (string?)null,
                WordCount = 200,
                CreatedBy = (Guid?)null,
                Visibility = Visibility.Private,
                MeaningCheckpoints = Array.Empty<object>(),
                SeededErrors = Array.Empty<object>(),
                IsSeedReference = true,
            });
            Assert.Equal(HttpStatusCode.Created, importResponse.StatusCode);
            var importedSeed = await importResponse.Content.ReadFromJsonAsync<ImportUserQuestionResult>();

            var generateResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.Questions.Base}/generate",
                new
                {
                    ExamTypeId = examType!.Id,
                    TaskType = TaskType.A,
                    Difficulty = Difficulty.medium,
                    SeedQuestionIds = new[] { importedSeed!.Id },
                    CreatedBy = (Guid?)null,
                });
            Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);
            var generated = await generateResponse.Content.ReadFromJsonAsync<GenerateQuestionResult>();

            var linksResponse = await client.GetAsync($"{ApiRoutes.Questions.Base}/{generated!.Id}/seed-references");
            var links = await linksResponse.Content.ReadFromJsonAsync<List<SeedReferenceLinkResultItem>>();

            Assert.Contains(links!, l => l.SeedQuestionId == importedSeed!.Id);
        }

        /// <summary>
        /// Few-shot samples are strictly opt-in (user-requested): with a perfectly matching
        /// IsSeedReference question in the bank but NO SeedQuestionIds on the request, the
        /// generated question must have zero seed-reference links — the handler no longer
        /// auto-selects seeds, so "真题参考样本" never enters the prompt uninvited.
        /// </summary>
        [Fact]
        public async Task Generate_without_seed_question_ids_records_no_seed_references_even_when_a_matching_seed_exists()
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
                await context.Questions.AddAsync(new Question
                {
                    Id = Guid.NewGuid(),
                    TaskType = TaskType.A,
                    Difficulty = Difficulty.medium,
                    Title = $"real_exam_seed_{Guid.NewGuid():N}",
                    SourceText = "A matching real-exam passage the handler must NOT auto-pick.",
                    Origin = QuestionOrigin.real_exam_seed,
                    SourceType = SourceType.real_exam,
                    IsSeedReference = true,
                    Visibility = Visibility.Private,
                    CreatedAt = DateTimeOffset.UtcNow,
                });
                await context.SaveChangesAsync();
            }

            var generateResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.Questions.Base}/generate",
                new { ExamTypeId = examType!.Id, TaskType = TaskType.A, Difficulty = Difficulty.medium, CreatedBy = (Guid?)null });
            Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);
            var generated = await generateResponse.Content.ReadFromJsonAsync<GenerateQuestionResult>();

            var linksResponse = await client.GetAsync($"{ApiRoutes.Questions.Base}/{generated!.Id}/seed-references");
            var links = await linksResponse.Content.ReadFromJsonAsync<List<SeedReferenceLinkResultItem>>();

            Assert.Empty(links!);
        }

        /// <summary>
        /// Self-audit fix (2026-08-30, design doc §4.2's retry sub-state-machine): before this,
        /// GenerateQuestionCommandHandler treated a single malformed AI response as an immediate,
        /// unretried final_failure. This proves the fix end to end through the real API + real
        /// Postgres: an LLM client that returns garbage twice then a valid response the third time
        /// still yields a 201 Created, and the persisted AiCallLog row's AttemptCount reflects all
        /// 3 real attempts rather than showing 1 (the pre-fix behavior would never have reached a
        /// successful response at all).
        /// </summary>
        [Fact]
        public async Task Generate_retries_on_a_malformed_ai_response_and_succeeds_once_the_ai_returns_something_valid()
        {
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddScoped<ILlmClientResolver, FakeLlmClientResolverFailingTwiceThenSucceeding>()))
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
                new { ExamTypeId = examType!.Id, TaskType = TaskType.A, Difficulty = Difficulty.medium, CreatedBy = (Guid?)null });

            Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);
            var generated = await generateResponse.Content.ReadFromJsonAsync<GenerateQuestionResult>();
            Assert.Equal(FakeLlmClient.FixedTitle, generated!.Title);

            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var aiCallLog = await context.AiCallLogs.SingleAsync(
                x => x.RequestType == AiOperationType.question_gen && x.RelatedId == generated.Id);

            Assert.Equal(CallStatus.success, aiCallLog.Status);
            Assert.Equal(3, aiCallLog.AttemptCount);
        }

        private const string WeakPointHintMarkerTemplate = "WEAK_POINT_HINT_MARKER: {{ if weak_point_hint }}[{{ weak_point_hint }}]{{ end }}";

        private static async Task SeedWeakPointHintPromptTemplateAsync(ApiWebApplicationFactory factory)
        {
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.PromptTemplates.AddAsync(new PromptTemplate
            {
                Id = Guid.NewGuid(),
                SubjectCategory = SubjectCategory.translation,
                TemplateType = AiOperationType.question_gen,
                Layer = TemplateLayer.shared_methodology,
                TemplateContent = WeakPointHintMarkerTemplate,
                Version = 1,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Design doc §10.5 "出题与薄弱点联动", deliberately opt-in (WeakPointTargetingSelector's
        /// own doc comment). Default TargetWeakPoints=false must never inject a hint into the
        /// prompt, even when the user has an active, high-priority weak point on file — proves the
        /// feature really is off unless explicitly requested, not just "usually off by chance".
        /// </summary>
        [Fact]
        public async Task Generate_with_target_weak_points_false_never_injects_a_weak_point_hint_even_when_one_exists()
        {
            var capturingClient = new CapturingQuestionGenLlmClient();
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddScoped<ILlmClientResolver>(_ => new FixedQuestionGenLlmClientResolver(capturingClient))))
                .CreateClient();

            await SeedWeakPointHintPromptTemplateAsync(_factory);

            var examTypeResponse = await client.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, new
            {
                Code = $"test_{Guid.NewGuid():N}",
                Name = "API Test Exam Type",
                SubjectCategory = SubjectCategory.translation,
            });
            var examType = await examTypeResponse.Content.ReadFromJsonAsync<CreateExamTypeResult>();

            var userId = await _factory.SeedUserAsync();
            var category = $"weak_category_{Guid.NewGuid():N}";
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await context.WeakPoints.AddAsync(new WeakPoint
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Category = category,
                    Status = WeakPointStatus.active,
                    Priority = Priority.high,
                    FirstDetectedAt = DateTimeOffset.UtcNow,
                    LastSeenAt = DateTimeOffset.UtcNow,
                });
                await context.SaveChangesAsync();
            }

            var generateResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.Questions.Base}/generate",
                new { ExamTypeId = examType!.Id, TaskType = TaskType.A, Difficulty = Difficulty.medium, CreatedBy = userId, TargetWeakPoints = false });

            Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);
            Assert.DoesNotContain(category, capturingClient.CapturedPrompt);
        }

        /// <summary>
        /// The other half of the same guarantee: TargetWeakPoints=true, plus forcing the
        /// weak_point_targeting_ratio policy to 1.0 (always target — same "override the seeded
        /// policy with a degenerate distribution" technique as
        /// Generate_uses_the_seeded_difficulty_distribution_policy_when_difficulty_is_omitted"),
        /// really does inject the user's top-priority active weak point's category into the prompt.
        /// </summary>
        [Fact]
        public async Task Generate_with_target_weak_points_true_and_a_forced_ratio_of_one_injects_the_users_top_weak_point_hint()
        {
            var capturingClient = new CapturingQuestionGenLlmClient();
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddScoped<ILlmClientResolver>(_ => new FixedQuestionGenLlmClientResolver(capturingClient))))
                .CreateClient();

            await SeedWeakPointHintPromptTemplateAsync(_factory);

            var examTypeResponse = await client.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, new
            {
                Code = $"test_{Guid.NewGuid():N}",
                Name = "API Test Exam Type",
                SubjectCategory = SubjectCategory.translation,
            });
            var examType = await examTypeResponse.Content.ReadFromJsonAsync<CreateExamTypeResult>();

            var userId = await _factory.SeedUserAsync();
            var lowPriorityCategory = $"weak_low_{Guid.NewGuid():N}";
            var highPriorityCategory = $"weak_high_{Guid.NewGuid():N}";
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await context.WeakPoints.AddRangeAsync(
                    new WeakPoint
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Category = lowPriorityCategory,
                        Status = WeakPointStatus.active,
                        Priority = Priority.low,
                        FirstDetectedAt = DateTimeOffset.UtcNow,
                        LastSeenAt = DateTimeOffset.UtcNow,
                    },
                    new WeakPoint
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Category = highPriorityCategory,
                        Status = WeakPointStatus.active,
                        Priority = Priority.high,
                        FirstDetectedAt = DateTimeOffset.UtcNow,
                        LastSeenAt = DateTimeOffset.UtcNow,
                    });
                await context.GenerationPolicies.AddAsync(new GenerationPolicy
                {
                    Id = Guid.NewGuid(),
                    ExamTypeId = examType!.Id,
                    PolicyKey = "weak_point_targeting_ratio",
                    PolicyValue = "{\"weak_point_ratio\": 1.0}",
                    UpdatedAt = DateTimeOffset.UtcNow,
                });
                await context.SaveChangesAsync();
            }

            var generateResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.Questions.Base}/generate",
                new { ExamTypeId = examType!.Id, TaskType = TaskType.A, Difficulty = Difficulty.medium, CreatedBy = userId, TargetWeakPoints = true });

            Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);
            Assert.Contains(highPriorityCategory, capturingClient.CapturedPrompt);
            Assert.DoesNotContain(lowPriorityCategory, capturingClient.CapturedPrompt);
        }

        private const string DomainListMarkerTemplate =
            "DOMAIN_LIST_MARKER:{{ for d in domain_categories }}[{{ d.name }}]{{ end }}";

        private static async Task SeedDomainListPromptTemplateAsync(ApiWebApplicationFactory factory)
        {
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.PromptTemplates.AddAsync(new PromptTemplate
            {
                Id = Guid.NewGuid(),
                SubjectCategory = SubjectCategory.translation,
                TemplateType = AiOperationType.question_gen,
                Layer = TemplateLayer.shared_methodology,
                TemplateContent = DomainListMarkerTemplate,
                Version = 1,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// The existing question_bank_categories(domain) names are injected into the prompt so the
        /// AI's brief.domain reuses one instead of inventing near-duplicates. Seeds two uniquely
        /// named domain categories and asserts both names reach the rendered prompt.
        /// </summary>
        [Fact]
        public async Task Generate_injects_existing_domain_category_names_into_the_prompt()
        {
            var capturingClient = new CapturingQuestionGenLlmClient();
            var client = _factory
                .WithWebHostBuilder(builder => builder.ConfigureTestServices(
                    services => services.AddScoped<ILlmClientResolver>(_ => new FixedQuestionGenLlmClientResolver(capturingClient))))
                .CreateClient();

            await SeedDomainListPromptTemplateAsync(_factory);

            var examTypeResponse = await client.PostAsJsonAsync(ApiRoutes.ExamTypes.Base, new
            {
                Code = $"test_{Guid.NewGuid():N}",
                Name = "API Test Exam Type",
                SubjectCategory = SubjectCategory.translation,
            });
            var examType = await examTypeResponse.Content.ReadFromJsonAsync<CreateExamTypeResult>();

            var domainA = $"domain_a_{Guid.NewGuid():N}";
            var domainB = $"domain_b_{Guid.NewGuid():N}";
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await context.QuestionBankCategories.AddRangeAsync(
                    new QuestionBankCategory { Id = Guid.NewGuid(), CategoryType = CategoryType.domain, Name = domainA, CreatedAt = DateTimeOffset.UtcNow },
                    new QuestionBankCategory { Id = Guid.NewGuid(), CategoryType = CategoryType.domain, Name = domainB, CreatedAt = DateTimeOffset.UtcNow });
                await context.SaveChangesAsync();
            }

            var generateResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.Questions.Base}/generate",
                new { ExamTypeId = examType!.Id, TaskType = TaskType.A, Difficulty = Difficulty.medium, CreatedBy = (Guid?)null });

            Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);
            Assert.Contains(domainA, capturingClient.CapturedPrompt);
            Assert.Contains(domainB, capturingClient.CapturedPrompt);
        }

        /// <summary>
        /// The double-link fix: when the caller pins a CategoryId, that is the question's ONLY
        /// question_category_map link. The AI's brief.domain ("test" from the fake client) must
        /// not also spawn/link a second domain category — MapCategoriesAsync used to add both.
        /// </summary>
        [Fact]
        public async Task Generate_with_a_pinned_category_links_only_that_category_and_not_a_second_from_brief_domain()
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

            Guid pinnedCategoryId;
            using (var scope = _factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var category = new QuestionBankCategory
                {
                    Id = Guid.NewGuid(),
                    CategoryType = CategoryType.domain,
                    // deliberately != FakeLlmClient's brief.domain ("test")
                    Name = $"pinned_domain_{Guid.NewGuid():N}",
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                await context.QuestionBankCategories.AddAsync(category);
                await context.SaveChangesAsync();
                pinnedCategoryId = category.Id;
            }

            var generateResponse = await client.PostAsJsonAsync(
                $"{ApiRoutes.Questions.Base}/generate",
                new
                {
                    ExamTypeId = examType!.Id,
                    TaskType = TaskType.A,
                    Difficulty = Difficulty.medium,
                    CategoryId = pinnedCategoryId,
                    CreatedBy = (Guid?)null,
                });
            Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);
            var generated = await generateResponse.Content.ReadFromJsonAsync<GenerateQuestionResult>();

            var getResponse = await client.GetAsync($"{ApiRoutes.Questions.Base}/{generated!.Id}");
            var question = await getResponse.Content.ReadFromJsonAsync<GetQuestionByIdResult>();

            var linkedCategoryId = Assert.Single(question!.CategoryIds);
            Assert.Equal(pinnedCategoryId, linkedCategoryId);
        }
    }
}
