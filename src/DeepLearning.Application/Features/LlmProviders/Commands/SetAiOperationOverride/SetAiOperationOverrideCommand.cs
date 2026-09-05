using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.LlmProviders.Commands.SetAiOperationOverride
{
    /// <summary>
    /// Pins <paramref name="OperationType"/> to <paramref name="ProviderKey"/>, regardless of
    /// which provider is globally active. Full replace, not a patch: <paramref name="Model"/>
    /// null means "follow that provider's own current model", <paramref name="ThinkingEnabled"/>
    /// null means "follow that provider's own ThinkingEnabled", and <paramref name="Effort"/>
    /// null means "follow that provider's own Effort" — passing null for any of these clears a
    /// previously-set value back to that default, it does not leave it untouched. See
    /// AiOperationProviderOverride's doc comment.
    /// </summary>
    public record SetAiOperationOverrideCommand(
        AiOperationType OperationType,
        string ProviderKey,
        string? Model,
        bool? ThinkingEnabled,
        string? Effort) : IRequest<SetAiOperationOverrideResult>;
}
