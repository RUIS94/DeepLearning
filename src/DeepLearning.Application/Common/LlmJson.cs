using System.Text.Json;

namespace DeepLearning.Application.Common
{
    /// <summary>
    /// Strip an LLM's markdown code-fence wrapper and parse the JSON payload underneath. Single
    /// source for a pattern that used to be hand-copied byte-for-byte into 6 places (Infra's
    /// weak-point/vocab-drift services via the old PromptJsonHelper, plus GenerateQuestion /
    /// GenerateDeepLearningContent / GenerateProgressTrendSnapshot / FollowUpThreadSupport /
    /// GradeSubmission each keeping their own copy) — see 代码复用扫描_07_优化计划.md §2.2.
    /// </summary>
    public static class LlmJson
    {
        public static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

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

        public static T Parse<T>(string rawText)
        {
            var json = StripMarkdownFence(rawText.Trim());
            return JsonSerializer.Deserialize<T>(json, CaseInsensitive)
                ?? throw new InvalidOperationException("Deserialized to null.");
        }
    }
}
