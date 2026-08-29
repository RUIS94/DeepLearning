namespace DeepLearning.Application.Features.LlmProviders.Commands.AddLlmProviderModel
{
    public record AddLlmProviderModelResult(
        string ProviderKey,
        string Model,
        string? Label,
        bool IsCurrent,
        DateTimeOffset CreatedAt);
}
