namespace DeepLearning.Application.Common
{
    /// <summary>
    /// Keys for the <c>feature_flags</c> table (design doc §六's "功能开关" / §11.2 Step 10) —
    /// the 题库 / 复习库 surfaces are gated so they can be dark-launched per environment without a
    /// redeploy. Nothing read this table until the 2026-09-09 Phase 5 round.
    /// </summary>
    public static class FeatureFlags
    {
        public const string QuestionBankEnabled = "question_bank_enabled";
        public const string ReviewLibraryEnabled = "review_library_enabled";

        /// <summary>
        /// Value used when a key has no row at all — chosen so a database that has never seen a
        /// <c>feature_flags</c> row behaves exactly as it did before this was wired in (both
        /// features on). An unknown key defaults to <c>false</c> (fail closed).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, bool> Defaults = new Dictionary<string, bool>
        {
            [QuestionBankEnabled] = true,
            [ReviewLibraryEnabled] = true,
        };

        public static bool DefaultFor(string key) => Defaults.TryGetValue(key, out var value) && value;
    }
}
