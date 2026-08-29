using MediatR;

namespace DeepLearning.Application.Features.LlmProviders.Queries.ListLlmProviderModels
{
    public record ListLlmProviderModelsQuery(string ProviderKey) : IRequest<List<LlmProviderModelResultItem>>;
}
