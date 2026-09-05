using System.Text.Json.Serialization;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.LlmProviders.Queries.ListAiOperationOverrides
{
    /// <summary>
    /// One row per <see cref="AiOperationType"/>, always — not just the ones with an override —
    /// so an admin surface can show every operation's current resolution in one list.
    /// <see cref="ProviderKey"/> null means "follows the globally active provider" (in which
    /// case <see cref="Model"/>/<see cref="ThinkingEnabled"/>/<see cref="Effort"/> are always
    /// null too — there is no override row at all). When <see cref="ProviderKey"/> is set,
    /// <see cref="Model"/> null means "follow that provider's own current model",
    /// <see cref="ThinkingEnabled"/> null means "follow that provider's own ThinkingEnabled",
    /// and <see cref="Effort"/> null means "follow that provider's own Effort".
    /// </summary>
    public record AiOperationOverrideResultItem(
        [property: JsonConverter(typeof(JsonStringEnumConverter))] AiOperationType OperationType,
        string? ProviderKey,
        string? Model,
        bool? ThinkingEnabled,
        string? Effort,
        DateTimeOffset? UpdatedAt);
}
