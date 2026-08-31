using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.FollowUps.Queries.ListFollowUpQuestions
{
    public record FollowUpQuestionResultItem(
        Guid Id,
        Guid SubmissionId,
        Guid UserId,
        string? ContextRef,
        string QuestionText,
        string? AiResponse,
        FollowUpVerdict Verdict,
        DateTimeOffset CreatedAt);
}
