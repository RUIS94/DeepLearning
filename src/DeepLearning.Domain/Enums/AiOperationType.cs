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
        weak_point_classification,
        // Rare, batched call fired only when a weak point first crosses the 3-submission
        // tracking threshold, or resurfaces after being resolved: generates the executable
        // "how to spot this trap in a fresh source text" rule stored on WeakPoint.DetectionCriteria.
        // See 薄弱点分类与生命周期管理_策划书.md §3 — not run on every submission, unlike
        // weak_point_classification.
        weak_point_detection_criteria,
        // Post-grading call that checks the user's active weak points NOT hit by this
        // submission's classification against this submission's source text + translation,
        // batched into one call for all such codes. Returns resolved / still_weak / not_present
        // per code — never writes error_list or increments RecurrenceCount/OccurrenceSubmissionCount
        // (策划书 §2/§3). Skipped entirely when the user has no active weak points to recheck.
        weak_point_recheck
    }
}
