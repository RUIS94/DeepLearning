using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.FollowUps.Queries.GetFollowUpQuestionById
{
    public record GetFollowUpQuestionByIdResult(
        Guid Id,
        Guid SubmissionId,
        Guid UserId,
        string? ContextRef,
        string QuestionText,
        string? AiResponse,
        FollowUpVerdict Verdict,
        DateTimeOffset CreatedAt);
}
