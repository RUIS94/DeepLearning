using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateErrorTaxonomy;
using DeepLearning.Application.Features.ExamConfig.Commands.CreateExamType;
using DeepLearning.Application.Features.Questions.Commands.ImportUserQuestion;
using DeepLearning.Application.Features.Questions.Queries.GetQuestionById;
using DeepLearning.Domain.Enums;
using DeepLearning.UnitTests.TestInfrastructure;

namespace DeepLearning.UnitTests.Api
{
    [Collection(ApiCollection.Name)]
    public class QuestionsControllerTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public QuestionsControllerTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Task_a_question_response_has_no_task_b_details()
        {
            var client = _factory.CreateClient();

            var createResponse = await client.PostAsJsonAsync(ApiRoutes.Questions.Base, new
            {
                TaskType = TaskType.A,
                Difficulty = Difficulty.medium,
                Title = "API Test TaskA Question",
                Brief = (string?)null,
                SourceText = "Some source text to translate.",
                FlawedTranslationText = (string?)null,
                CreatedBy = (Guid?)null,
                Visibility = Visibility.Private,
                MeaningCheckpoints = new[] { new { CheckpointText = "Must convey X.", CheckpointType = (string?)null, Importance = CheckpointImportance.core } },
                SeededErrors = Array.Empty<object>(),
            });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<ImportUserQuestionResult>();

            var getResponse = await client.GetAsync($"{ApiRoutes.Questions.Base}/{created!.Id}");
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            var fetched = await getResponse.Content.ReadFromJsonAsync<GetQuestionByIdResult>();
            Assert.Null(fetched!.TaskB);
            Assert.Single(fetched.MeaningCheckpoints);
        }

        [Fact]
        public async Task Task_b_question_response_includes_task_b_details_with_seeded_errors()
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

            var taxonomyResponse = await client.PostAsJsonAsync(
                ApiRoutes.ErrorTaxonomies.Base.Replace("{examTypeId:guid}", examType!.Id.ToString()),
                new { CategoryKey = "distortion", CategoryName = "Distortion", Description = (string?)null, ExampleCases = (string?)null });
            taxonomyResponse.EnsureSuccessStatusCode();
            var taxonomy = await taxonomyResponse.Content.ReadFromJsonAsync<CreateErrorTaxonomyResult>();

            const string flawedText = "This sentence has an error in it.";
            var createResponse = await client.PostAsJsonAsync(ApiRoutes.Questions.Base, new
            {
                TaskType = TaskType.B,
                Difficulty = Difficulty.medium,
                Title = "API Test TaskB Question",
                Brief = (string?)null,
                SourceText = "Original source text.",
                FlawedTranslationText = flawedText,
                CreatedBy = (Guid?)null,
                Visibility = Visibility.Private,
                MeaningCheckpoints = Array.Empty<object>(),
                SeededErrors = new[] { new { PositionStart = 9, PositionEnd = 17, ErrorTaxonomyId = taxonomy!.Id, CorrectReferenceText = "had", Note = (string?)null } },
            });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<ImportUserQuestionResult>();

            var getResponse = await client.GetAsync($"{ApiRoutes.Questions.Base}/{created!.Id}");
            var fetched = await getResponse.Content.ReadFromJsonAsync<GetQuestionByIdResult>();

            Assert.NotNull(fetched!.TaskB);
            Assert.Equal(flawedText, fetched.TaskB!.FlawedTranslationText);
            Assert.Single(fetched.TaskB.SeededErrors);
            Assert.Equal("distortion", fetched.TaskB.SeededErrors[0].ErrorCategoryKey);
        }

        [Fact]
        public async Task Create_returns_400_when_task_a_carries_seeded_errors()
        {
            var client = _factory.CreateClient();

            var response = await client.PostAsJsonAsync(ApiRoutes.Questions.Base, new
            {
                TaskType = TaskType.A,
                Difficulty = Difficulty.easy,
                Title = "Invalid TaskA",
                Brief = (string?)null,
                SourceText = "Some source text.",
                FlawedTranslationText = (string?)null,
                CreatedBy = (Guid?)null,
                Visibility = Visibility.Private,
                MeaningCheckpoints = Array.Empty<object>(),
                SeededErrors = new[] { new { PositionStart = 0, PositionEnd = 5, ErrorTaxonomyId = Guid.NewGuid(), CorrectReferenceText = "fix", Note = (string?)null } },
            });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        /// <summary>
        /// Self-audit follow-up (2026-08-30, user-requested): before this, nothing in the API
        /// could ever produce a Question with IsSeedReference=true — the whole few-shot/
        /// traceability mechanism (design doc §11.2 Step 8) was only reachable by writing
        /// directly to the DB. This is now the one way in: importing with IsSeedReference=true
        /// also sets Origin/SourceType to the real_exam_seed/real_exam pair instead of the usual
        /// user_uploaded/user_generated one.
        /// </summary>
        [Fact]
        public async Task Import_with_is_seed_reference_true_sets_origin_and_source_type_to_the_real_exam_pair()
        {
            var client = _factory.CreateClient();

            var createResponse = await client.PostAsJsonAsync(ApiRoutes.Questions.Base, new
            {
                TaskType = TaskType.A,
                Difficulty = Difficulty.medium,
                Title = "A real-exam sample article",
                Brief = (string?)null,
                SourceText = "Some real-exam-shaped source text.",
                FlawedTranslationText = (string?)null,
                CreatedBy = (Guid?)null,
                Visibility = Visibility.Private,
                MeaningCheckpoints = Array.Empty<object>(),
                SeededErrors = Array.Empty<object>(),
                IsSeedReference = true,
            });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<ImportUserQuestionResult>();

            var getResponse = await client.GetAsync($"{ApiRoutes.Questions.Base}/{created!.Id}");
            var fetched = await getResponse.Content.ReadFromJsonAsync<GetQuestionByIdResult>();

            Assert.True(fetched!.IsSeedReference);
            Assert.Equal(QuestionOrigin.real_exam_seed, fetched.Origin);
            Assert.Equal(SourceType.real_exam, fetched.SourceType);
        }

        [Fact]
        public async Task Import_without_specifying_is_seed_reference_defaults_to_false_and_the_ordinary_origin_pair()
        {
            var client = _factory.CreateClient();

            var createResponse = await client.PostAsJsonAsync(ApiRoutes.Questions.Base, new
            {
                TaskType = TaskType.A,
                Difficulty = Difficulty.medium,
                Title = "An ordinary user-uploaded question",
                Brief = (string?)null,
                SourceText = "Some ordinary source text.",
                FlawedTranslationText = (string?)null,
                CreatedBy = (Guid?)null,
                Visibility = Visibility.Private,
                MeaningCheckpoints = Array.Empty<object>(),
                SeededErrors = Array.Empty<object>(),
            });
            var created = await createResponse.Content.ReadFromJsonAsync<ImportUserQuestionResult>();

            var getResponse = await client.GetAsync($"{ApiRoutes.Questions.Base}/{created!.Id}");
            var fetched = await getResponse.Content.ReadFromJsonAsync<GetQuestionByIdResult>();

            Assert.False(fetched!.IsSeedReference);
            Assert.Equal(QuestionOrigin.user_uploaded, fetched.Origin);
            Assert.Equal(SourceType.user_generated, fetched.SourceType);
        }

        [Fact]
        public async Task Import_derives_word_count_from_the_source_text_ignoring_the_title()
        {
            var client = _factory.CreateClient();

            var createResponse = await client.PostAsJsonAsync(ApiRoutes.Questions.Base, new
            {
                TaskType = TaskType.A,
                Difficulty = Difficulty.medium,
                Title = "A four word title here",
                Brief = (string?)null,
                SourceText = "The quick brown fox jumps over the lazy dog.",
                FlawedTranslationText = (string?)null,
                CreatedBy = (Guid?)null,
                Visibility = Visibility.Private,
                MeaningCheckpoints = Array.Empty<object>(),
                SeededErrors = Array.Empty<object>(),
            });
            Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<ImportUserQuestionResult>();

            var getResponse = await client.GetAsync($"{ApiRoutes.Questions.Base}/{created!.Id}");
            var fetched = await getResponse.Content.ReadFromJsonAsync<GetQuestionByIdResult>();

            Assert.Equal(9, fetched!.WordCount);
        }

        [Fact]
        public async Task List_returns_ok()
        {
            var client = _factory.CreateClient();

            var response = await client.GetAsync(ApiRoutes.Questions.Base);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
