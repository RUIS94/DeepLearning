using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.Submissions.Queries.ListSubmissions
{
    public record ListSubmissionsResultItem(
        Guid Id,
        Guid QuestionId,
        TaskType TaskType,
        SubmissionStatus Status,
        DateTimeOffset? SubmittedAt,
        DateTimeOffset CreatedAt);
}
