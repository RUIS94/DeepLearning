using System.Text.Json.Serialization;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.LlmProviders.Commands.SetAiOperationOverride
{
    public record SetAiOperationOverrideResult(
        [property: JsonConverter(typeof(JsonStringEnumConverter))] AiOperationType OperationType,
        string ProviderKey,
        string? Model,
        bool? ThinkingEnabled,
        string? Effort,
        DateTimeOffset UpdatedAt);
}
