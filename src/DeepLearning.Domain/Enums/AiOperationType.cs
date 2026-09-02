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
        followup_summary,
        // One AI call after grading that maps the submission's errors to weak_point_catalog
        // codes — used only for the cases the deterministic (dimension, error category) rule
        // can't separate (several distinct weak-point kinds all landing on the same
        // dimension/category pair). Optional: no template configured -> the rule handles
        // everything, exactly as before this existed.
        weak_point_classification
    }
}
