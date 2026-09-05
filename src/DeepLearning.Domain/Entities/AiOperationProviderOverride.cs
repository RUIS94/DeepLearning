using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Entities
{
    /// <summary>
    /// Pins one <see cref="AiOperationType"/> to a specific provider, independent of whichever
    /// provider is globally <see cref="LlmProviderSettings.IsActive"/>. A row here means "always
    /// run this operation through ProviderKey, using that provider's own current model and its
    /// own ThinkingEnabled/Effort/ExtraSettings" (see <see cref="LlmProviderSettings"/> read via
    /// GetByProviderKeyAsync, not GetActiveAsync). No row for an operation type means "follow the
    /// global active provider", exactly as before this table existed — this is additive, not a
    /// replacement for llm_provider_settings.IsActive.
    /// </summary>
    public class AiOperationProviderOverride : Entity
    {
        public AiOperationType OperationType { get; set; }

        public string ProviderKey { get; set; } = string.Empty;

        /// <summary>Null = use ProviderKey's own current LlmProviderModel (IsCurrent=true), same as everything else that doesn't ask for a specific model. Set = pin this operation to that exact model under ProviderKey regardless of which one is currently "current" for the provider.</summary>
        public string? Model { get; set; }

        /// <summary>Null = use ProviderKey's own LlmProviderSettings.ThinkingEnabled. Set = override thinking for this operation only, independent of that provider's global default.</summary>
        public bool? ThinkingEnabled { get; set; }

        /// <summary>Null = use ProviderKey's own LlmProviderSettings.Effort. Set = override effort for this operation only. Meaningful for Claude today (output_config.effort) and harmlessly ignored by adapters that don't read LlmCompletionRequest.Effort — see ILlmClient's doc comment.</summary>
        public string? Effort { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
