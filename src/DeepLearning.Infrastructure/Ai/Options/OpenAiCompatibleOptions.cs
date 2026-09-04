namespace DeepLearning.Infrastructure.Ai.Options
{
    /// <summary>
    /// Config shape shared by every provider whose HTTP API follows the OpenAI Chat
    /// Completions wire format (OpenAI itself, DeepSeek, Mimo — verified against each
    /// provider's own docs on 2026-08-29, not assumed). The three differ only in base URL,
    /// auth header style, model id, and the request field name for the output-length cap —
    /// captured here as config rather than code so no new C# is needed to add a fourth.
    /// </summary>
    public class OpenAiCompatibleOptions
    {
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>Full endpoint URL (not just a host) — e.g. "https://api.openai.com/v1/chat/completions".</summary>
        public string BaseUrl { get; set; } = string.Empty;

        public string Model { get; set; } = string.Empty;

        /// <summary>"Authorization" for OpenAI/DeepSeek (Bearer-style); "api-key" for Mimo.</summary>
        public string AuthHeaderName { get; set; } = "Authorization";

        /// <summary>Prepended to ApiKey in the auth header — "Bearer " for OpenAI/DeepSeek, "" for Mimo.</summary>
        public string AuthHeaderValuePrefix { get; set; } = "Bearer ";

        /// <summary>"max_tokens" (DeepSeek) or "max_completion_tokens" (OpenAI, Mimo) — differs per provider docs.</summary>
        public string MaxTokensFieldName { get; set; } = "max_completion_tokens";

        /// <summary>
        /// Request field carrying this provider's reasoning switch, emitted as
        /// <c>{"&lt;name&gt;": {"type": "enabled|disabled"}}</c>. Mimo calls it "thinking"
        /// (mimo.mi.com → Usage Guide → Deep Thinking, supported on mimo-v2.5-pro and
        /// mimo-v2.5). Null for providers with no such switch or a different shape — OpenAI's
        /// reasoning models use separate model ids, DeepSeek's is a separate model name — and
        /// nothing is sent when it is null, which is the pre-existing behaviour.
        ///
        /// <para>Worth setting where it exists, because on Mimo reasoning is ON by default and
        /// has two consequences the caller cannot see: it is billed inside
        /// <c>max_completion_tokens</c> together with the answer ("limits the total length of
        /// thinking content and the final answer"), and while it is on the model "does not
        /// support custom temperature and top_p" — they are forced to 1.0 / 0.95. So a call
        /// that asks for temperature 0 in the name of reproducibility silently does not get
        /// it.</para>
        /// </summary>
        public string? ThinkingParameterName { get; set; }
    }
}
