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
    /// GET /questions carries the *caller's own* attempt count + latest submission id;
    /// GET /submissions?questionId= lists the caller's own submissions newest-first. Neither
    /// endpoint accepts a userId query param anymore (ref/管理员与用户权限隔离_策划书.md Phase 1)
    /// — "my" always means the JWT's own identity, never an arbitrary id a caller can pass in.
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
                Visibility = Visibility.Private,
                MeaningCheckpoints = Array.Empty<object>(),
                SeededErrors = Array.Empty<object>(),
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<ImportUserQuestionResult>())!.Id;
        }

        private static Task<HttpResponseMessage> CreateSubmissionAsync(HttpClient client, Guid questionId) =>
            client.PostAsJsonAsync(ApiRoutes.Submissions.Base, new
            {
                QuestionId = questionId,
                TaskType = TaskType.A,
                Content = "\"my translation\"",
            });

        [Fact]
        public async Task Question_list_reports_the_callers_own_attempt_count_and_latest_submission()
        {
            var client = _factory.CreateAuthenticatedClient();
            var questionId = await ImportTaskAQuestionAsync(client);

            var first = await CreateSubmissionAsync(client, questionId);
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
            var firstId = (await first.Content.ReadFromJsonAsync<CreateSubmissionResult>())!.Id;

            var second = await CreateSubmissionAsync(client, questionId);
            Assert.Equal(HttpStatusCode.Created, second.StatusCode);
            var secondId = (await second.Content.ReadFromJsonAsync<CreateSubmissionResult>())!.Id;

            var list = await client.GetFromJsonAsync<List<ListQuestionsResultItem>>(ApiRoutes.Questions.Base);
            var row = Assert.Single(list!, q => q.Id == questionId);
            Assert.Equal(2, row.MyAttemptCount);
            Assert.Contains(row.MyLatestSubmissionId, new[] { (Guid?)firstId, secondId });

            // A different authenticated caller can't even see this question — it's private to its
            // creator (U4, ref/管理员与用户权限隔离_策划书.md), a stronger isolation guarantee than
            // "no attempts recorded".
            var otherClient = _factory.CreateAuthenticatedClient();
            var otherList = await otherClient.GetFromJsonAsync<List<ListQuestionsResultItem>>(ApiRoutes.Questions.Base);
            Assert.DoesNotContain(otherList!, q => q.Id == questionId);
        }

        [Fact]
        public async Task Submission_list_returns_only_the_callers_own_submissions_for_a_question_newest_first()
        {
            var client = _factory.CreateAuthenticatedClient();
            var otherClient = _factory.CreateAuthenticatedClient();
            var questionId = await ImportTaskAQuestionAsync(client);
            var otherQuestionId = await ImportTaskAQuestionAsync(client);

            var a = await CreateSubmissionAsync(client, questionId);
            var b = await CreateSubmissionAsync(client, questionId);
            await CreateSubmissionAsync(client, otherQuestionId); // different question — excluded
            await CreateSubmissionAsync(otherClient, questionId); // different user — excluded
            var aId = (await a.Content.ReadFromJsonAsync<CreateSubmissionResult>())!.Id;
            var bId = (await b.Content.ReadFromJsonAsync<CreateSubmissionResult>())!.Id;

            var list = await client.GetFromJsonAsync<List<ListSubmissionsResultItem>>(
                $"{ApiRoutes.Submissions.Base}?questionId={questionId}");

            Assert.Equal(2, list!.Count);
            Assert.All(list, s => Assert.Equal(questionId, s.QuestionId));
            Assert.Equal(new HashSet<Guid> { aId, bId }, list.Select(s => s.Id).ToHashSet());
            // newest-first: CreatedAt is non-increasing down the list
            Assert.True(list[0].CreatedAt >= list[1].CreatedAt);
        }
    }
}
