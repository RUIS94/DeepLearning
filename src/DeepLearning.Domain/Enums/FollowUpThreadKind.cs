namespace DeepLearning.Domain.Enums
{
    /// <summary>
    /// What a follow-up thread is FOR — decides how much context each round's AI call carries
    /// and which closing call it gets. A submission still has at most one open thread at a time
    /// (see <see cref="Entities.FollowUpThread"/>), so this is fixed per thread, with one
    /// allowed transition: <see cref="knowledge"/> → <see cref="dispute"/> when the first
    /// round's reply reports it was actually a challenge (disputeDetected) — never the reverse,
    /// because a knowledge payload is a strict subset of a dispute payload.
    /// </summary>
    public enum FollowUpThreadKind
    {
        /// <summary>
        /// A plain question about the material — a phrasing choice, a term, how to split a long
        /// sentence. Round context is just source + submission + reference translation +
        /// question + history. Never triggers a re-grade or a standardRevision.
        /// </summary>
        knowledge,

        /// <summary>
        /// A challenge to a specific finding — "this was Major not Minor", "you missed an error
        /// here". Adds the error list, the existing grading results, the disputed dimension's
        /// Band text and the Major/Minor definitions. The closing followup_summary call may
        /// record a standardRevision, but still does NOT change this submission's score.
        /// </summary>
        dispute,

        /// <summary>
        /// A challenge to a dimension's Band itself — "one small slip, why is Accuracy Band 3".
        /// Started only from the score display, anchored to <see cref="Entities.FollowUpThread.DimensionId"/>.
        /// Carries that dimension's full Band ladder; its closing call may trigger the re-grade
        /// flow (rewrite the Band, leave an audit row). Does NOT emit a standardRevision.
        /// Reserved here; wired up in a later phase.
        /// </summary>
        score_challenge,
    }
}
