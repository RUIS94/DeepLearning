using System.Text;
using System.Text.Json;

namespace DeepLearning.Application.Common
{
    /// <summary>
    /// Strip an LLM's markdown code-fence wrapper and parse the JSON payload underneath. Single
    /// source for a pattern that used to be hand-copied byte-for-byte into 6 places (Infra's
    /// weak-point/vocab-drift services via the old PromptJsonHelper, plus GenerateQuestion /
    /// GenerateDeepLearningContent / GenerateProgressTrendSnapshot / FollowUpThreadSupport /
    /// GradeSubmission each keeping their own copy) — see 代码复用扫描_07_优化计划.md §2.2.
    /// Being the one shared entry point, a repair added here (see <see cref="Parse{T}"/>) covers
    /// every one of those call sites at once.
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

        /// <summary>
        /// Parses the LLM's JSON, falling back to <see cref="RepairStrayQuotesAndControlChars"/>
        /// once if the fast path rejects it — observed failure: the model quotes a term inside an
        /// explanation/suggestion string with a plain ASCII `"` instead of escaping it (or a
        /// Chinese quote 「」), which ends the string early; everything from there on reads as
        /// stray tokens and System.Text.Json reports a byte deep in the following prose (e.g.
        /// "'0xE7' is invalid after a value") that has nothing to do with the actual mistake. The
        /// repair pass never runs on JSON that already parses, so it cannot regress the common
        /// case — it only gets a second, corrected attempt at input that was already going to
        /// throw.
        /// </summary>
        public static T Parse<T>(string rawText)
        {
            var json = StripMarkdownFence(rawText.Trim());
            try
            {
                return Deserialize<T>(json);
            }
            catch (JsonException)
            {
                return Deserialize<T>(RepairStrayQuotesAndControlChars(json));
            }
        }

        private static T Deserialize<T>(string json)
            => JsonSerializer.Deserialize<T>(json, CaseInsensitive)
                ?? throw new InvalidOperationException("Deserialized to null.");

        /// <summary>
        /// Best-effort repair for two failure classes that produce syntactically-broken-but-
        /// obviously-intended JSON: an unescaped `"` inside a string value (most often the model
        /// quoting a word/phrase in its own explanation text), and a raw control character
        /// (newline/tab/CR) inside a string value — both are legal in prose but illegal inside a
        /// JSON string. Walks the text tracking whether we're inside a string; a `"` encountered
        /// there is treated as the string's real closing quote only if the next non-whitespace
        /// character looks structural (`,` `}` `]` `:`, or end of input) — otherwise it's escaped
        /// in place and the string continues. This is a heuristic, not a JSON grammar: a string
        /// that legitimately ends right before a stray ASCII comma/colon inside CJK prose (rare —
        /// Chinese punctuation is full-width, ，：) can still be misjudged. It is only ever invoked
        /// after strict parsing already failed, so a wrong guess here is no worse than the
        /// unrecoverable failure that already happened.
        /// </summary>
        internal static string RepairStrayQuotesAndControlChars(string json)
        {
            var sb = new StringBuilder(json.Length + 16);
            var inString = false;

            for (var i = 0; i < json.Length; i++)
            {
                var c = json[i];

                if (!inString)
                {
                    sb.Append(c);
                    if (c == '"')
                    {
                        inString = true;
                    }
                    continue;
                }

                if (c == '\\' && i + 1 < json.Length)
                {
                    sb.Append(c).Append(json[i + 1]);
                    i++;
                    continue;
                }

                switch (c)
                {
                    case '"' when LooksLikeStringEnd(json, i + 1):
                        sb.Append(c);
                        inString = false;
                        break;
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        private static bool LooksLikeStringEnd(string json, int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index]))
            {
                index++;
            }

            return index >= json.Length || json[index] is ',' or '}' or ']' or ':';
        }
    }
}
