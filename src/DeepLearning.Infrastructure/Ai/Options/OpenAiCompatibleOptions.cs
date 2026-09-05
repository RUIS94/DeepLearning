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
        /// <c>{"&lt;name&gt;": {"type": "enabled|disabled"}}</c>. Both Mimo ("thinking" —
        /// mimo.mi.com → Usage Guide → Deep Thinking, supported on mimo-v2.5-pro and
        /// mimo-v2.5) and DeepSeek's current v4 models ("thinking", confirmed against
        /// DeepSeek's own docs on 2026-09-06 — deepseek-v4-flash/deepseek-v4-pro toggle
        /// reasoning on the *same* model id this way; an earlier version of this comment
        /// wrongly assumed DeepSeek only exposed reasoning via a separate model name, e.g.
        /// "deepseek-reasoner" — that was true of an older DeepSeek generation, not v4) use
        /// this shape. Null for a provider with no such switch or a genuinely different shape,
        /// in which case nothing is sent — the pre-existing behaviour.
        ///
        /// <para>Leaving this unset for a provider that does support the switch is not a
        /// no-op: DeepSeek and Mimo both default reasoning to ON (DeepSeek at effort "high")
        /// when the field is omitted, so a call that asks for reasoning to be off (grading's
        /// determinism requirement, or a caller that has already blown its token budget on a
        /// truncation retry) silently keeps reasoning on regardless of what
        /// LlmProviderSettings.ThinkingEnabled says — the request never carries the field that
        /// would have said otherwise. Confirmed on 2026-09-06: DeepSeek question-gen calls
        /// with ThinkingEnabled=false still reasoned by default and blew the output-token cap
        /// on every retry, because DeepSeek's entry here had no ThinkingParameterName set.</para>
        ///
        /// <para>Mimo has an added wrinkle while reasoning is on: it is billed inside
        /// <c>max_completion_tokens</c> together with the answer ("limits the total length of
        /// thinking content and the final answer"), and the model "does not support custom
        /// temperature and top_p" — they are forced to 1.0 / 0.95. So a call that asks for
        /// temperature 0 in the name of reproducibility silently does not get it.</para>
        /// </summary>
        public string? ThinkingParameterName { get; set; }

        /// <summary>
        /// Request field carrying this provider's reasoning-strength control, emitted as a bare
        /// string value (e.g. <c>{"&lt;name&gt;": "high"}</c>) — a separate knob from
        /// <see cref="ThinkingParameterName"/>'s on/off switch. DeepSeek calls it
        /// "reasoning_effort" (confirmed against DeepSeek's own docs on 2026-09-06) and accepts
        /// the same low/medium/high/xhigh/max scale <c>LlmProviderSettings.Effort</c> already
        /// uses for Claude's <c>output_config.effort</c> — DeepSeek maps medium/high/xhigh all
        /// onto its own internal "high" bucket, but that collapsing happens on DeepSeek's side,
        /// not ours, so this is a straight passthrough of whatever string an admin has set. Null
        /// for a provider with no such field, or one not verified against its docs yet (e.g.
        /// Mimo — its effort/reasoning-strength control, if any, has not been checked) — nothing
        /// is sent when it is null, same as ThinkingParameterName.
        /// </summary>
        public string? ReasoningEffortFieldName { get; set; }
    }
}
