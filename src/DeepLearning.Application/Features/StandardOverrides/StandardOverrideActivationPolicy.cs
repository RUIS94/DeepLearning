using System.Text.Json;

namespace DeepLearning.Application.Features.StandardOverrides
{
    /// <summary>
    /// Design doc §10.6: an observing standard_overrides row (an AI-judgment correction note —
    /// see IStandardOverrideRepository's doc comment for why this is not a rubric rewrite) is
    /// promoted to active once the same correction has been independently confirmed (by a
    /// follow-up whose verdict was user_correct) on this many distinct questions. Pure/static so
    /// the threshold judgment itself is testable without a database — same convention as
    /// DifficultyDistributionSelector. Originally lived under FollowUps/Commands/
    /// CreateFollowUpQuestion; moved here when that single-shot command was retired in favor of
    /// FollowUpThreads (CloseFollowUpThreadCommandHandler is now the sole caller).
    /// </summary>
    public static class StandardOverrideActivationPolicy
    {
        /// <summary>Used when no override_activation_threshold policy row exists yet for the exam type — design doc §10.6's own "如3次" example.</summary>
        public const int DefaultConfirmationsRequired = 3;

        public static bool ShouldActivate(int distinctQuestionConfirmations, int confirmationsRequired)
            => distinctQuestionConfirmations >= confirmationsRequired;

        public static int ParseThreshold(string policyValueJson)
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, int>>(policyValueJson)
                ?? throw new InvalidOperationException("override_activation_threshold policy_value could not be parsed as a JSON object.");

            return raw.TryGetValue("confirmations_required", out var value) && value > 0
                ? value
                : throw new InvalidOperationException("override_activation_threshold policy_value must contain a positive 'confirmations_required' number.");
        }
    }
}
