using System.Text.Json;

namespace DeepLearning.Application.Features.Questions.Commands.GenerateQuestion
{
    /// <summary>
    /// Design doc §10.5's "出题与薄弱点联动" — deliberately optional (design doc's own "不追求
    /// 100%围绕薄弱点出题,否则出题会变得单一,也测不出其他方面有没有退步"), gated two ways:
    /// GenerateQuestionCommand.TargetWeakPoints must be explicitly true (caller opts in per call —
    /// see GenerateQuestionCommandHandler), AND, even then, only a weak_point_ratio-weighted roll
    /// of the dice actually targets a weak point on any given call. generation_policy's
    /// weak_point_targeting_ratio row ({"weak_point_ratio":0.3,"random_ratio":0.7}, already seeded
    /// by seed_naati_ct_en_zh.sql) supplies the ratio; ParseRatio/DefaultWeakPointRatio mirror
    /// DifficultyDistributionSelector's own parse/fallback convention.
    ///
    /// Pure/static: ShouldTarget takes the roll as a parameter instead of touching Random itself,
    /// so tests can drive it deterministically instead of asserting on real randomness.
    /// </summary>
    public static class WeakPointTargetingSelector
    {
        public const double DefaultWeakPointRatio = 0.3;

        /// <param name="roll">A value in [0, 1) — pass a real Random.Shared.NextDouble() in production.</param>
        public static bool ShouldTarget(double weakPointRatio, double roll) => roll < weakPointRatio;

        public static double ParseRatio(string policyValueJson)
        {
            var raw = JsonSerializer.Deserialize<Dictionary<string, double>>(policyValueJson)
                ?? throw new InvalidOperationException("weak_point_targeting_ratio policy_value could not be parsed as a JSON object of numbers.");

            return raw.TryGetValue("weak_point_ratio", out var ratio) ? ratio : DefaultWeakPointRatio;
        }
    }
}
