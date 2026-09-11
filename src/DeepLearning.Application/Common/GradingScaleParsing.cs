using System.Text.RegularExpressions;

namespace DeepLearning.Application.Common
{
    /// <summary>
    /// Pulls a leading number out of a grading-scale string like "Band 2" or "Band 2 or above" —
    /// shared by all three IGradingResultInterpreter implementations (was an `internal` class
    /// living inside Band15Interpreter.cs, promoted to `public` here so it has one home instead of
    /// also being hand-copied — LINQ-char, no negative-sign support — inside
    /// GradeSubmissionCommandHandler; 代码复用扫描_07_优化计划.md §3.2).
    /// </summary>
    public static class GradingScaleParsing
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
