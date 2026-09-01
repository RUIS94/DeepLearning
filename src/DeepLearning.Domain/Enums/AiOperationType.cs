namespace DeepLearning.Domain.Enums
{
    public enum AiOperationType
    {
        question_gen,
        grading,
        followup,
        standard_revision,
        deep_learning,
        progress_trend,
        // Follow-up threads (FollowUpThreads/Commands/CloseFollowUpThread) run a separate
        // prompt_templates row from per-round 'followup' replies: only this summary call is
        // allowed to return a verdict/standardRevision with real side effects (see
        // FollowUpThread's own doc comment for why the summary call, not each round, decides
        // the outcome).
        followup_summary
    }
}
