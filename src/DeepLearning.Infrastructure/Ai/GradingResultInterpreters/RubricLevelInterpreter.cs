using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Infrastructure.Ai.GradingResultInterpreters
{
    /// <summary>
    /// No current exam type uses rubric_level either (see Score100Interpreter's note) — this
    /// reuses band_1_5's numeric, lower-is-better convention as the placeholder interpretation
    /// (level_descriptions rows always key "1".."5" today, band_1_5 included) rather than
    /// inventing an ordered level-name vocabulary with no real data to validate it against.
    /// Revisit once a second exam type actually uses this scale.
    /// </summary>
    public class RubricLevelInterpreter : IGradingResultInterpreter
    {
        public ScaleType ScaleType => ScaleType.rubric_level;

        public GradingInterpretation Interpret(string rawValue, string? passThreshold)
        {
            var level = GradingScaleParsing.ExtractLeadingInt(rawValue)
                ?? throw new InvalidOperationException($"Could not parse a level number out of '{rawValue}'.");
            var thresholdLevel = GradingScaleParsing.ExtractLeadingInt(passThreshold);

            return new GradingInterpretation(level, thresholdLevel is null || level <= thresholdLevel);
        }
    }
}
