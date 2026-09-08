using DeepLearning.Application.Features.FollowUpThreads;
using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.FollowUpThreads.Commands.CloseFollowUpThread
{
    /// <summary>
    /// User-triggered "结束追问 / 结束改判申请".
    ///
    /// <para>Input is the user-reviewed (and possibly edited) summary — the frontend first calls
    /// <see cref="Queries.PreviewFollowUpClose.PreviewFollowUpCloseQuery"/> to get an AI draft,
    /// shows it for confirm / edit / regenerate / discard, and only on confirm sends it back
    /// here. When Input is supplied no AI call happens; it is validated and committed as-is.
    /// When Input is null the handler falls back to running the AI summary itself and committing
    /// its output (the old one-shot behaviour, kept for tests / a "just close it" path).</para>
    ///
    /// <para>A pure-knowledge thread (never a dispute, never score_challenge) has nothing to
    /// adjudicate: it closes with FinalVerdict = null and no AI call at all, whatever Input says.</para>
    ///
    /// <para><see cref="SkipSummary"/> = "close this dispute / score_challenge without generating
    /// anything": no AI call, no verdict, no StandardOverride, no Band rewrite — it closes exactly
    /// like a knowledge thread (FinalVerdict = null, submission back to graded). Input is ignored
    /// when it is set.</para>
    ///
    /// Never reopened afterwards (single-thread-per-submission).
    /// </summary>
    public record CloseFollowUpThreadCommand(
        Guid ThreadId,
        Guid UserId,
        FollowUpCloseInput? Input = null,
        bool SkipSummary = false) : IRequest<FollowUpThreadResult>;

    /// <summary>
    /// The reviewed summary the user is committing. Shape covers both close kinds; the handler
    /// reads the fields relevant to <see cref="Domain.Entities.FollowUpThread.Kind"/>.
    /// </summary>
    public record FollowUpCloseInput(
        string AiResponse,
        // dispute (followup_summary)
        FollowUpVerdict? FinalVerdict,
        StandardRevisionInput? StandardRevision,
        // score_challenge (score_challenge_summary)
        ScoreChallengeDecision? Decision,
        int? RevisedBand,
        string? RevisedRationale);

    public record StandardRevisionInput(
        OverrideScope Scope,
        string DimensionOrRule,
        string? OriginalRuleText,
        string RevisedRuleText);
}
