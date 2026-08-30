using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateErrorTaxonomy;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateExamType;
using DeepLearning.Application.Features.Questions.Commands.GenerateQuestion;
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
        /// (可追溯性测试)". Seeds one matching real-exam sample (same task type + difficulty) and
        /// one non-matching one directly into the DB, generates a question, and asserts the
        /// traceability endpoint reports exactly the matching seed — proving
        /// GenerateQuestionCommandHandler's few-shot retrieval and SeedReferenceLink persistence
        /// are wired together end to end, not just documented.
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
                var nonMatchingSeed = new Question
                {
                    Id = Guid.NewGuid(),
                    TaskType = TaskType.A,
                    Difficulty = Difficulty.hard, // wrong difficulty — must not be referenced
                    Title = $"real_exam_seed_{Guid.NewGuid():N}",
                    SourceText = "A different-difficulty real-exam passage.",
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
                new { ExamTypeId = examType!.Id, TaskType = TaskType.A, Difficulty = Difficulty.medium, CreatedBy = (Guid?)null });
            Assert.Equal(HttpStatusCode.Created, generateResponse.StatusCode);
            var generated = await generateResponse.Content.ReadFromJsonAsync<GenerateQuestionResult>();

            var linksResponse = await client.GetAsync($"{ApiRoutes.Questions.Base}/{generated!.Id}/seed-references");
            Assert.Equal(HttpStatusCode.OK, linksResponse.StatusCode);
            var links = await linksResponse.Content.ReadFromJsonAsync<List<SeedReferenceLinkResultItem>>();

            Assert.Single(links!);
            Assert.Equal(matchingSeed.Id, links![0].SeedQuestionId);
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
    }
}
