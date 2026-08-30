using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.FollowUps.Commands.CreateFollowUpQuestion
{
    public record CreateFollowUpQuestionResult(
        Guid Id,
        Guid SubmissionId,
        FollowUpVerdict Verdict,
        string AiResponse,
        SubmissionStatus SubmissionStatus,
        Guid? StandardOverrideId,
        OverrideStatus? StandardOverrideStatus);
}
