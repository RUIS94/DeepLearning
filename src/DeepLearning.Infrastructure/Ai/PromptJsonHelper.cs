namespace DeepLearning.Infrastructure.Ai
{
    /// <summary>Shared parsing helper for the small JSON-returning weak-point AI calls (classifier, detection-criteria generator, recheck).</summary>
    internal static class PromptJsonHelper
    {
        public static string StripMarkdownFence(string text)
        {
            if (!text.StartsWith("```", StringComparison.Ordinal))
            {
                return text;
            }

            var firstNewLine = text.IndexOf('\n');
            var withoutOpeningFence = firstNewLine >= 0 ? text[(firstNewLine + 1)..] : text;
            var closingFenceIndex = withoutOpeningFence.LastIndexOf("```", StringComparison.Ordinal);
            return closingFenceIndex >= 0 ? withoutOpeningFence[..closingFenceIndex] : withoutOpeningFence;
        }
    }
}
