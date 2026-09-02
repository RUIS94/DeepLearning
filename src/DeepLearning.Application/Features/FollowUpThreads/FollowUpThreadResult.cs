using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.FollowUpThreads
{
    /// <summary>
    /// Compact row for the "this submission's follow-up threads" list (GET /follow-up-threads?submissionId=).
    /// FirstQuestion is the opening user message, for a scannable label; full messages come from
    /// GET /follow-up-threads/{id}.
    /// </summary>
    public record FollowUpThreadSummary(
        Guid Id,
        FollowUpThreadStatus Status,
        FollowUpVerdict? FinalVerdict,
        Guid? StandardOverrideId,
        int MessageCount,
        string FirstQuestion,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ClosedAt)
    {
        public static FollowUpThreadSummary From(FollowUpThread thread) => new(
            thread.Id,
            thread.Status,
            thread.FinalVerdict,
            thread.StandardOverrideId,
            thread.Messages.Count,
            thread.Messages.FirstOrDefault(m => m.Role == FollowUpMessageRole.user)?.Content ?? string.Empty,
            thread.CreatedAt,
            thread.ClosedAt);
    }

    /// <summary>Shared response shape returned by the create / add-message / close / get-by-id endpoints.</summary>
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
