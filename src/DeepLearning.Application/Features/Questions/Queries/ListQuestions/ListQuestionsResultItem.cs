using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.Questions.Queries.ListQuestions
{
    public record ListQuestionsResultItem(
        Guid Id,
        TaskType TaskType,
        Difficulty Difficulty,
        string Title,
        int? WordCount,
        bool InBank,
        DateTimeOffset CreatedAt,
        int MyAttemptCount,
        Guid? MyLatestSubmissionId);
}
