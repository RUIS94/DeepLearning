using System.Text.Json;

namespace DeepLearning.Application.Features.Questions.Commands.GenerateQuestion
{
    /// <summary>
    /// Design doc §11.2 Step 8's "题材可随机": when the caller does NOT pin a CategoryId, a
    /// topic_random_ratio-weighted roll decides whether to nudge this generation toward one
    /// randomly-picked existing domain category (a soft prompt hint, not a hard filter) or to
    /// leave the AI free to choose the subject. generation_policy's <c>topic_distribution</c>
    /// row ({"topic_random_ratio":0.5}) supplies the ratio; ParseRatio/DefaultTopicRandomRatio
    /// mirror DifficultyDistributionSelector / WeakPointTargetingSelector's parse+fallback
    /// convention. Pure/static — ShouldPick takes the roll as a parameter so tests drive it
    /// deterministically.
    /// </summary>
    public static class TopicDistributionSelector
    {
        public const double DefaultTopicRandomRatio = 0.5;

        /// <param name="roll">A value in [0, 1) — pass a real Random.Shared.NextDouble() in production.</param>
        public static bool ShouldPick(double topicRandomRatio, double roll) => roll < topicRandomRatio;

        public static double ParseRatio(string policyValueJson)
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, double>>(policyValueJson)
                ?? throw new InvalidOperationException("topic_distribution policy_value could not be parsed as a JSON object of numbers.");

            return raw.TryGetValue("topic_random_ratio", out var ratio) ? ratio : DefaultTopicRandomRatio;
        }
    }
}
