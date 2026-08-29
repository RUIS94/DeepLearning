using System.Text.Json;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.Questions.Commands.GenerateQuestion
{
    /// <summary>
    /// Weighted-random difficulty pick from generation_policy's difficulty_distribution row
    /// (seeded as {"easy":0.3,"medium":0.5,"hard":0.2} — design doc §2's "出题难度分配比例",
    /// §11.2's出题策略配置). Used only when the caller doesn't specify a Difficulty explicitly —
    /// an explicit request always wins outright, this never overrides one.
    ///
    /// Pure/static: Select takes the roll as a parameter instead of touching Random itself, so
    /// tests can drive it deterministically instead of asserting on real randomness.
    /// </summary>
    public static class DifficultyDistributionSelector
    {
        /// <summary>Used when no difficulty_distribution row exists for the exam type yet — matches the ratio documented in the design doc/source prompt, so behavior is sensible even before anyone seeds the policy row.</summary>
        public static readonly IReadOnlyDictionary<Difficulty, decimal> DefaultWeights = new Dictionary<Difficulty, decimal>
        {
            [Difficulty.easy] = 0.3m,
            [Difficulty.medium] = 0.5m,
            [Difficulty.hard] = 0.2m,
        };

        /// <param name="roll">A value in [0, 1) — pass a real Random.Shared.NextDouble() in production.</param>
        public static Difficulty Select(IReadOnlyDictionary<Difficulty, decimal> weights, double roll)
        {
            var total = weights.Values.Sum();
            if (total <= 0)
            {
                throw new InvalidOperationException("Difficulty distribution weights must sum to a positive value.");
            }

            var target = (decimal)roll * total;
            decimal cumulative = 0;
            foreach (var (difficulty, weight) in weights)
            {
                cumulative += weight;
                if (target < cumulative)
                {
                    return difficulty;
                }
            }

            // Floating-point rounding at roll ~= 1.0 could otherwise fall through with no match.
            return weights.Keys.Last();
        }

        public static IReadOnlyDictionary<Difficulty, decimal> ParseWeights(string policyValueJson)
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, decimal>>(policyValueJson)
                ?? throw new InvalidOperationException("difficulty_distribution policy_value could not be parsed as a JSON object of numbers.");

            var weights = new Dictionary<Difficulty, decimal>();
            foreach (var (key, value) in raw)
            {
                if (Enum.TryParse<Difficulty>(key, out var difficulty))
                {
                    weights[difficulty] = value;
                }
            }

            if (weights.Count == 0)
            {
                throw new InvalidOperationException("difficulty_distribution policy_value contained no recognized Difficulty keys (easy/medium/hard).");
            }

            return weights;
        }
    }
}
