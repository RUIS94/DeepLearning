using System.Text.RegularExpressions;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Infrastructure.Ai.GradingResultInterpreters
{
    /// <summary>
    /// NAATI CT's own scale — the only one with real seed data today. Band 1's text always
    /// describes the best performance and Band 5 the worst (see level_descriptions in
    /// seed_naati_ct_en_zh.sql), so "Pass: Band 2 or above" reads as "Band 2 or numerically
    /// better" — pass_bool = reported band &lt;= the threshold's band number.
    /// </summary>
    public class Band15Interpreter : IGradingResultInterpreter
    {
        public ScaleType ScaleType => ScaleType.band_1_5;

        public GradingInterpretation Interpret(string rawValue, string? passThreshold)
        {
            var band = GradingScaleParsing.ExtractLeadingInt(rawValue)
                ?? throw new InvalidOperationException($"Could not parse a band number out of '{rawValue}'.");
            var thresholdBand = GradingScaleParsing.ExtractLeadingInt(passThreshold);

            return new GradingInterpretation(band, thresholdBand is null || band <= thresholdBand);
        }
    }

    internal static class GradingScaleParsing
    {
        private static readonly Regex LeadingIntPattern = new(@"-?\d+(\.\d+)?", RegexOptions.Compiled);

        public static int? ExtractLeadingInt(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var match = LeadingIntPattern.Match(text);
            return match.Success ? (int)double.Parse(match.Value) : null;
        }

        public static decimal? ExtractLeadingDecimal(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var match = LeadingIntPattern.Match(text);
            return match.Success ? decimal.Parse(match.Value) : null;
        }
    }
}
