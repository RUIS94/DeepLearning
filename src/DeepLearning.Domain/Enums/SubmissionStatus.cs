namespace DeepLearning.Domain.Enums
{
    public enum SubmissionStatus
    {
        draft,
        submitted,
        grading,
        grading_failed,
        graded,
        under_dispute,
        standard_revised,
        archived,
        grading_abandoned,
        // Appended last on purpose: the frontend mirrors this enum by ordinal (see
        // enums.ts), and JSON serialises it as a number, so a mid-enum insert would shift
        // archived/grading_abandoned. A score_challenge follow-up thread closed with
        // decision=adjust: the disputed dimension's Band was rewritten in place and a
        // GradingResultRevision audit row written. Distinct from graded so the UI can badge
        // "已改判"; a re-graded submission can still be challenged again (regraded -> under_dispute).
        regraded
    }
}
