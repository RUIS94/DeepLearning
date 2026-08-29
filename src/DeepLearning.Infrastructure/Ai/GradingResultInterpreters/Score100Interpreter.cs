using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Infrastructure.Ai.GradingResultInterpreters
{
    /// <summary>
    /// No current exam type uses score_0_100 (NAATI CT is entirely band_1_5) — this exists so
    /// IGradingResultInterpreter's per-scale_type dispatch is real today rather than a stub for
    /// a hypothetical future exam type (design doc §7's "IGradingResultInterpreter策略接口"),
    /// but its bucketing choice below is a documented placeholder, not verified against a real
    /// rubric. Unlike band_1_5, higher is better here — pass_bool = raw score &gt;= threshold.
    /// </summary>
    public class Score100Interpreter : IGradingResultInterpreter
    {
        public ScaleType ScaleType => ScaleType.score_0_100;

        public GradingInterpretation Interpret(string rawValue, string? passThreshold)
        {
            var score = GradingScaleParsing.ExtractLeadingDecimal(rawValue)
                ?? throw new InvalidOperationException($"Could not parse a numeric score out of '{rawValue}'.");
            var threshold = GradingScaleParsing.ExtractLeadingDecimal(passThreshold);

            var band = score switch
            {
                >= 90 => 1,
                >= 75 => 2,
                >= 60 => 3,
                >= 40 => 4,
                _ => 5,
            };

            return new GradingInterpretation(band, threshold is null || score >= threshold);
        }
    }
}
