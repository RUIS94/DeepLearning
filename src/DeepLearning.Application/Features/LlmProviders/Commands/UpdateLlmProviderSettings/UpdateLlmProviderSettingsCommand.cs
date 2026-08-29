using MediatR;

namespace DeepLearning.Application.Features.LlmProviders.Commands.UpdateLlmProviderSettings
{
    /// <summary>
    /// Partial update — only the fields the caller actually sets get changed. This is what
    /// backs "turn thinking on/off", "set Claude's effort", and "attach a provider-specific
    /// extra_settings blob" without touching anything else on the row. Switching which model
    /// this provider currently uses is NOT here — that's SelectLlmProviderModelCommand, since
    /// the model lives on LlmProviderModel, not on this row.
    /// </summary>
    public record UpdateLlmProviderSettingsCommand(
        string ProviderKey,
        bool? ThinkingEnabled,
        string? Effort,
        string? ExtraSettingsJson) : IRequest<UpdateLlmProviderSettingsResult>;
}
