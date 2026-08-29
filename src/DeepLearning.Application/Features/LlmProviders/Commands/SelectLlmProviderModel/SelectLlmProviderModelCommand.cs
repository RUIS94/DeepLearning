using MediatR;

namespace DeepLearning.Application.Features.LlmProviders.Commands.SelectLlmProviderModel
{
    /// <summary>Makes an already-cataloged model the current one for its provider. Does not add a new catalog entry — see AddLlmProviderModelCommand for that.</summary>
    public record SelectLlmProviderModelCommand(string ProviderKey, string Model) : IRequest<SelectLlmProviderModelResult>;
}
