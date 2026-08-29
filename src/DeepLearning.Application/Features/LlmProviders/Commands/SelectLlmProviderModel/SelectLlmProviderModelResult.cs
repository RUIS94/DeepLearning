namespace DeepLearning.Application.Features.LlmProviders.Commands.SelectLlmProviderModel
{
    public record SelectLlmProviderModelResult(string ProviderKey, string Model, bool IsCurrent);
}
