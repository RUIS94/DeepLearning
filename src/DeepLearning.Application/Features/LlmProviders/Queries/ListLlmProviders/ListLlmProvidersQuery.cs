using MediatR;

namespace DeepLearning.Application.Features.LlmProviders.Queries.ListLlmProviders
{
    public record ListLlmProvidersQuery : IRequest<List<LlmProviderResultItem>>;
}
