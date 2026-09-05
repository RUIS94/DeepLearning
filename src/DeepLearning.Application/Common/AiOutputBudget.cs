namespace DeepLearning.Application.Common
{
    /// <summary>
    /// Named token-budget tiers for <see cref="AdaptiveCompletionRunner"/> call sites, so a call
    /// site declares "this is a medium-length task" instead of picking two more magic numbers.
    /// Each tier's <c>*Max</c> is the ceiling a truncated attempt's budget-doubling stops at —
    /// bounded further in practice by <c>AiCallLog.MaxRetries</c> (usually 3), so a task rarely
    /// actually reaches the max before it either succeeds or gives up.
    ///
    /// Sized off what each shape of task actually needs, not a mathematical progression — see
    /// each call site for why it landed in its tier (in short: progress_trend's one narrative
    /// paragraph is UltraShort; weak_point_*'s per-error assignments + per-code summaries is
    /// Short; a single structured reply/question payload is Medium; deep_learning's reference
    /// translation + notes + patterns + vocab list is Long; grading's full error list with
    /// rationale across many findings is UltraLong).
    /// </summary>
    public static class AiOutputBudget
    {
        public const int UltraShortInitial = 1024;
        public const int UltraShortMax = 4096;

        public const int ShortInitial = 2048;
        public const int ShortMax = 8192;

        public const int MediumInitial = 4096;
        public const int MediumMax = 8192;

        public const int LongInitial = 8192;
        public const int LongMax = 16384;

        public const int UltraLongInitial = 16384;
        public const int UltraLongMax = 32768;
    }
}
