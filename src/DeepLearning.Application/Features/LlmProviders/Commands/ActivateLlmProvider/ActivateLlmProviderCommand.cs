using MediatR;

namespace DeepLearning.Application.Features.LlmProviders.Commands.ActivateLlmProvider
{
    public record ActivateLlmProviderCommand(string ProviderKey) : IRequest<ActivateLlmProviderResult>;
}
