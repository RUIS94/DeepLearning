namespace DeepLearning.Application.Features.LlmProviders.Queries.ListLlmProviders
{
    public record LlmProviderResultItem(
        string ProviderKey,
        bool IsActive,

        /// <summary>The provider's current LlmProviderModel row's Model, or null if none is set — read-only here, switch via SelectLlmProviderModelCommand.</summary>
        string? CurrentModel,
        bool ThinkingEnabled,
        string? Effort,
        string? ExtraSettings,
        DateTimeOffset UpdatedAt);
}
