using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.Questions.Queries.ListQuestions
{
    public class ListQuestionsQueryHandler : IRequestHandler<ListQuestionsQuery, List<ListQuestionsResultItem>>
    {
        private readonly IQuestionRepository _questionRepository;
        private readonly ISubmissionRepository _submissionRepository;

        public ListQuestionsQueryHandler(
            IQuestionRepository questionRepository,
            ISubmissionRepository submissionRepository)
        {
            _questionRepository = questionRepository;
            _submissionRepository = submissionRepository;
        }

        public async Task<List<ListQuestionsResultItem>> Handle(ListQuestionsQuery request, CancellationToken cancellationToken)
        {
            var questions = (await _questionRepository.ListAsync(
                request.TaskType, request.Difficulty, request.InBank, request.CategoryId,
                request.IsSeedReference, cancellationToken))
                // Every caller is authenticated now (ref/管理员与用户权限隔离_策划书.md U4) — the
                // bank's shared pool plus whatever the caller privately owns, never someone else's
                // private questions. request.UserId is always populated by the controller
                // (ICurrentUserService.RequiredUserId), so this isn't an opt-in filter.
                .Where(q => q.Visibility == Visibility.Shared || q.CreatedBy == request.UserId)
                .ToList();

            // Per-user attempt info is opt-in (only when a user id is supplied). One query for all
            // of this user's submissions, grouped in memory — MVP scale, no per-question fan-out.
            var attemptsByQuestion = request.UserId is { } userId
                ? (await _submissionRepository.ListByUserAsync(userId, null, cancellationToken))
                    .GroupBy(s => s.QuestionId)
                    .ToDictionary(
                        g => g.Key,
                        g => (Count: g.Count(), LatestId: g.OrderByDescending(s => s.CreatedAt).First().Id))
                : new Dictionary<Guid, (int Count, Guid LatestId)>();

            return questions.Select(x =>
            {
                var hasAttempts = attemptsByQuestion.TryGetValue(x.Id, out var attempt);
                return new ListQuestionsResultItem(
                    x.Id, x.TaskType, x.Difficulty, x.Title, x.WordCount, x.InBank, x.CreatedAt,
                    hasAttempts ? attempt.Count : 0,
                    hasAttempts ? attempt.LatestId : null);
            }).ToList();
        }
    }
}
