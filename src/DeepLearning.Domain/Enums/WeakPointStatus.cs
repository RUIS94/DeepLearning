namespace DeepLearning.Domain.Enums
{
    /// <summary>
    /// <c>active</c>: confirmed (crossed the tracking threshold, or resurfaced after being
    /// <c>resolved</c> — the immediate-recurrence path bypasses the threshold entirely).
    /// <c>resolved</c>: the recheck call found evidence the learner has fixed it, or gave up
    /// finding evidence after 5 consecutive inconclusive checks (see
    /// <see cref="Entities.WeakPoint.NoEvidenceStreak"/>). <c>tracking</c>: newly detected, never
    /// yet crossed the 3-submission confirmation threshold — excluded from the grading prompt
    /// (that query filters on <c>active</c> only) and from the post-grading recheck.
    /// Appended last, not inserted, to keep the existing active=0/resolved=1 wire values stable —
    /// this enum has no <c>JsonStringEnumConverter</c>, so API responses serialize it as a plain
    /// ordinal int (see WeakPointStatus in the frontend's dtos.ts).
    /// </summary>
    public enum WeakPointStatus
    {
        active,
        resolved,
        tracking
    }
}
