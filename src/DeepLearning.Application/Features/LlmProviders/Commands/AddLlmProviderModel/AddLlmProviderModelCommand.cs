using MediatR;

namespace DeepLearning.Application.Features.LlmProviders.Commands.AddLlmProviderModel
{
    /// <summary>
    /// Adds a model to a provider's catalog. This never changes what's currently running for
    /// that provider — it's purely "this model is now known." Use SelectLlmProviderModelCommand
    /// to make a cataloged model the current one.
    /// </summary>
    public record AddLlmProviderModelCommand(
        string ProviderKey,
        string Model,
        string? Label) : IRequest<AddLlmProviderModelResult>;
}
