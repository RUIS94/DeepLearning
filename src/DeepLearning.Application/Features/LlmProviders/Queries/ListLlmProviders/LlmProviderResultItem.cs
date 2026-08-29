namespace DeepLearning.Application.Features.LlmProviders.Queries.ListLlmProviders
{
    public record LlmProviderResultItem(
        string ProviderKey,
        bool IsActive,
        string Model,
        bool ThinkingEnabled,
        string? Effort,
        string? ExtraSettings,
        DateTimeOffset UpdatedAt);
}
