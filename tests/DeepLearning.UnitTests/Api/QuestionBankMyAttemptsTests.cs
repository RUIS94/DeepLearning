using System.Net;
using System.Net.Http.Json;
using DeepLearning.Api.Constants;
using DeepLearning.Application.Features.Questions.Commands.ImportUserQuestion;
using DeepLearning.Application.Features.Questions.Queries.ListQuestions;
using DeepLearning.Application.Features.Submissions.Commands.CreateSubmission;
using DeepLearning.Application.Features.Submissions.Queries.ListSubmissions;
using DeepLearning.Domain.Enums;
using DeepLearning.UnitTests.TestInfrastructure;

namespace DeepLearning.UnitTests.Api
{
    /// <summary>
    /// P2: the question bank page shows "已练 N 次" per question and can open past attempts.
    /// GET /questions?userId= carries per-user attempt count + latest submission id;
    /// GET /submissions?userId=&amp;questionId= lists that user's submissions newest-first.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class QuestionBankMyAttemptsTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public QuestionBankMyAttemptsTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task<Guid> ImportTaskAQuestionAsync(HttpClient client)
        {
            var response = await client.PostAsJsonAsync(ApiRoutes.Questions.Base, new
            {
                TaskType = TaskType.A,
                Difficulty = Difficulty.medium,
                Title = $"Attempts test {Guid.NewGuid():N}",
                Brief = (string?)null,
                SourceText = "Some source text to translate.",
                FlawedTranslationText = (string?)null,
                WordCount = 10,
                CreatedBy = (Guid?)null,
                Visibility = Visibility.Private,
                MeaningCheckpoints = Array.Empty<object>(),
                SeededErrors = Array.Empty<object>(),
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<ImportUserQuestionResult>())!.Id;
        }

        private static Task<HttpResponseMessage> CreateSubmissionAsync(
            HttpClient client, Guid questionId, Guid userId) =>
            client.PostAsJsonAsync(ApiRoutes.Submissions.Base, new
            {
                QuestionId = questionId,
                UserId = userId,
                TaskType = TaskType.A,
                Content = "\"my translation\"",
            });

        [Fact]
        public async Task Question_list_with_user_id_reports_attempt_count_and_latest_submission()
        {
            var client = _factory.CreateClient();
            var userId = await _factory.SeedUserAsync();
            var questionId = await ImportTaskAQuestionAsync(client);

            var first = await CreateSubmissionAsync(client, questionId, userId);
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            var firstId = (await first.Content.ReadFromJsonAsync<CreateSubmissionResult>())!.Id;

            var second = await CreateSubmissionAsync(client, questionId, userId);
            Assert.Equal(HttpStatusCode.Created, second.StatusCode);
            var secondId = (await second.Content.ReadFromJsonAsync<CreateSubmissionResult>())!.Id;

            var list = await client.GetFromJsonAsync<List<ListQuestionsResultItem>>(
                $"{ApiRoutes.Questions.Base}?userId={userId}");
            var row = Assert.Single(list!, q => q.Id == questionId);
            Assert.Equal(2, row.MyAttemptCount);
            Assert.Contains(row.MyLatestSubmissionId, new[] { (Guid?)firstId, secondId });

            // Without a user id the per-user fields are absent (0 / null).
            var anon = await client.GetFromJsonAsync<List<ListQuestionsResultItem>>(ApiRoutes.Questions.Base);
            var anonRow = Assert.Single(anon!, q => q.Id == questionId);
            Assert.Equal(0, anonRow.MyAttemptCount);
            Assert.Null(anonRow.MyLatestSubmissionId);
        }

        [Fact]
        public async Task Submission_list_returns_the_users_submissions_for_a_question_newest_first()
        {
            var client = _factory.CreateClient();
            var userId = await _factory.SeedUserAsync();
            var otherUserId = await _factory.SeedUserAsync();
            var questionId = await ImportTaskAQuestionAsync(client);
            var otherQuestionId = await ImportTaskAQuestionAsync(client);

            var a = await CreateSubmissionAsync(client, questionId, userId);
            var b = await CreateSubmissionAsync(client, questionId, userId);
            await CreateSubmissionAsync(client, otherQuestionId, userId); // different question — excluded
            await CreateSubmissionAsync(client, questionId, otherUserId); // different user — excluded
            var aId = (await a.Content.ReadFromJsonAsync<CreateSubmissionResult>())!.Id;
            var bId = (await b.Content.ReadFromJsonAsync<CreateSubmissionResult>())!.Id;

            var list = await client.GetFromJsonAsync<List<ListSubmissionsResultItem>>(
                $"{ApiRoutes.Submissions.Base}?userId={userId}&questionId={questionId}");

            Assert.Equal(2, list!.Count);
            Assert.All(list, s => Assert.Equal(questionId, s.QuestionId));
            Assert.Equal(new HashSet<Guid> { aId, bId }, list.Select(s => s.Id).ToHashSet());
            // newest-first: CreatedAt is non-increasing down the list
            Assert.True(list[0].CreatedAt >= list[1].CreatedAt);
        }
    }
}
