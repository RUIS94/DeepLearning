using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.FollowUpThreads.Queries.PreviewFollowUpClose
{
    /// <summary>
    /// Runs the closing summary AI call over the whole thread and returns its output as a DRAFT
    /// — the thread stays open, the submission is untouched, no StandardOverride / re-grade
    /// happens. The frontend shows this for confirm / edit / regenerate / discard, then sends
    /// the reviewed version to CloseFollowUpThreadCommand.
    ///
    /// Rejected for a pure-knowledge thread — it has nothing to summarise (close it directly).
    /// </summary>
    public record PreviewFollowUpCloseQuery(Guid ThreadId, Guid UserId) : IRequest<FollowUpClosePreview>;

    /// <summary>
    /// AI draft of a thread close. Which fields are populated depends on Kind:
    /// dispute → AiResponse + FinalVerdict (+ StandardRevision when FinalVerdict = user_correct);
    /// score_challenge → AiResponse + Decision (+ RevisedBand / RevisedRationale when adjust).
    /// </summary>
    public record FollowUpClosePreview(
        FollowUpThreadKind Kind,
        string AiResponse,
        FollowUpVerdict? FinalVerdict,
        StandardRevisionPreview? StandardRevision,
        ScoreChallengeDecision? Decision,
        int? RevisedBand,
        string? RevisedRationale,
        // Handy context for the review UI on a score_challenge draft.
        int? CurrentBand,
        string? ChallengedDimensionKey);

    public record StandardRevisionPreview(
        OverrideScope Scope,
        string DimensionOrRule,
        string? OriginalRuleText,
        string RevisedRuleText);
}
