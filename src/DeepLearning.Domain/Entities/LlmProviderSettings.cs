using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    /// <summary>
    /// One row per known LLM provider ("claude"/"openai"/"deepseek"/"mimo"/...). Exactly one
    /// row may have IsActive=true at a time (enforced by a partial unique index) — that's the
    /// provider ILlmClientResolver hands out. Everything here is runtime-tunable without a
    /// redeploy: which provider is active, and per-provider generation controls. Which model
    /// that provider currently uses is NOT stored here — it lives on <see cref="LlmProviderModel"/>
    /// (IsCurrent=true row for this ProviderKey), so a provider's model catalog and its
    /// currently-selected model can never be two independently-editable, driftable values.
    /// Secrets (API keys, Claude's WorkspaceId) deliberately do NOT live here either — those
    /// are environment variables (see AGENTS.md's "AI integration" section).
    /// </summary>
    public class LlmProviderSettings : Entity
    {
        public string ProviderKey { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        /// <summary>Claude: whether to send thinking:{type:"disabled"} vs. letting it run adaptive. Not yet wired for other providers — their "thinking" mechanism differs per provider (e.g. a distinct model name) and isn't verified.</summary>
        public bool ThinkingEnabled { get; set; } = true;

        /// <summary>Claude's output_config.effort ("low"|"medium"|"high"|"xhigh"|"max"). Null = let Claude default. Not applicable to other providers.</summary>
        public string? Effort { get; set; }

        /// <summary>
        /// Free-form JSONB passthrough merged directly into the outgoing request body —
        /// the generic escape hatch for whatever provider-specific knob (temperature,
        /// reasoning_effort, top_p, ...) isn't a first-class column above.
        /// </summary>
        public string? ExtraSettings { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
