using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.LlmProviders.Commands.ClearAiOperationOverride
{
    /// <summary>Removes the pinned provider for <paramref name="OperationType"/>, so it goes back to following the globally active provider. A no-op (not an error) when no override exists.</summary>
    public record ClearAiOperationOverrideCommand(AiOperationType OperationType) : IRequest;
}
