namespace DeepLearning.Application.Features.LlmProviders.Queries.ListLlmProviderModels
{
    public record LlmProviderModelResultItem(
        string ProviderKey,
        string Model,
        string? Label,
        bool IsCurrent,
        DateTimeOffset CreatedAt);
}
