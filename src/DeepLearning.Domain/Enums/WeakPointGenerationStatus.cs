namespace DeepLearning.Domain.Enums
{
    /// <summary>
    /// How far the post-grading weak-point extraction has got for one submission.
    ///
    /// <para>Distinct from <see cref="WeakPointStatus"/>, which is about a weak point's own life
    /// cycle (active/resolved). This one is purely progress reporting: extraction makes its own
    /// LLM call, so it runs as a background job after grading rather than inside it, and the
    /// learner is shown a tag instead of being made to wait for a result they already have.</para>
    ///
    /// <para>Null on a submission means "not applicable" — it was graded before this existed, or
    /// has not been graded at all. That is deliberately different from <see cref="pending"/>,
    /// which promises a job is coming.</para>
    /// </summary>
    public enum WeakPointGenerationStatus
    {
        /// <summary>Grading is done and the job is queued, but has not started.</summary>
        pending,

        /// <summary>The job has it in hand.</summary>
        running,

        /// <summary>Finished. Includes "there were no errors to learn from", which is a success.</summary>
        succeeded,

        /// <summary>Gave up. The learner can ask for it again; the grading result is unaffected.</summary>
        failed,
    }
}
