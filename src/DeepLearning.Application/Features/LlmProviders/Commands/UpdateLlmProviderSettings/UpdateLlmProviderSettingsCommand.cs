using MediatR;

namespace DeepLearning.Application.Features.LlmProviders.Commands.UpdateLlmProviderSettings
{
    /// <summary>
    /// Partial update — only the fields the caller actually sets get changed. This is what
    /// backs "switch the model", "turn thinking on/off", "set Claude's effort", and "attach
    /// a provider-specific extra_settings blob" without touching anything else on the row.
    /// </summary>
    public record UpdateLlmProviderSettingsCommand(
        string ProviderKey,
        string? Model,
        bool? ThinkingEnabled,
        string? Effort,
        string? ExtraSettingsJson) : IRequest<UpdateLlmProviderSettingsResult>;
}
