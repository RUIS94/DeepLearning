using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.Submissions.Commands.CreateSubmission
{
    public record CreateSubmissionResult(
        Guid Id,
        Guid QuestionId,
        TaskType TaskType,
        SubmissionStatus Status,
        DateTimeOffset? SubmittedAt);
}
