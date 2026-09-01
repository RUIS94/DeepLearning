using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.FollowUpThreads
{
    /// <summary>Shared response shape returned by all four FollowUpThreads endpoints (create/add-message/close/get-by-submission).</summary>
    public record FollowUpThreadResult(
        Guid Id,
        Guid SubmissionId,
        Guid UserId,
        string? ContextRef,
        FollowUpThreadStatus Status,
        FollowUpVerdict? FinalVerdict,
        Guid? StandardOverrideId,
        OverrideStatus? StandardOverrideStatus,
        SubmissionStatus SubmissionStatus,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ClosedAt,
        List<FollowUpMessageResult> Messages)
    {
        public static FollowUpThreadResult From(FollowUpThread thread, SubmissionStatus submissionStatus, OverrideStatus? standardOverrideStatus) => new(
            thread.Id,
            thread.SubmissionId,
            thread.UserId,
            thread.ContextRef,
            thread.Status,
            thread.FinalVerdict,
            thread.StandardOverrideId,
            standardOverrideStatus,
            submissionStatus,
            thread.CreatedAt,
            thread.ClosedAt,
            thread.Messages.Select(FollowUpMessageResult.From).ToList());
    }

    public record FollowUpMessageResult(Guid Id, FollowUpMessageRole Role, string Content, FollowUpVerdict? Verdict, DateTimeOffset CreatedAt)
    {
        public static FollowUpMessageResult From(FollowUpMessage message) => new(
            message.Id, message.Role, message.Content, message.Verdict, message.CreatedAt);
    }
}
