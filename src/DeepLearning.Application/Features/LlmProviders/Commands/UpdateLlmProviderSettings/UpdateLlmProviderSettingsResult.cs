namespace DeepLearning.Application.Features.LlmProviders.Commands.UpdateLlmProviderSettings
{
    public record UpdateLlmProviderSettingsResult(
        string ProviderKey,
        bool IsActive,
        string Model,
        bool ThinkingEnabled,
        string? Effort,
        string? ExtraSettings,
        DateTimeOffset UpdatedAt);
}
