using System.Text.Json;

namespace DeepLearning.Application.Interfaces
{
    /// <summary>
    /// Provider-neutral abstraction over "call a large language model and get text back".
    /// Concrete adapters (Claude, OpenAI-compatible providers) live in Infrastructure/Ai and
    /// are selected via keyed DI — this interface and every caller of it stay completely
    /// unaware of which provider is active. Which provider is active, which model it uses,
    /// and the ThinkingEnabled/Effort/ExtraSettings defaults below are database-driven (see
    /// ILlmClientResolver + LlmProviderSettings) rather than fixed at startup.
    /// </summary>
    public interface ILlmClient
    {
        Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default);
    }

    public record LlmCompletionRequest(
        string? SystemPrompt,
        string UserPrompt,
        int MaxTokens,
        string? Model = null,
        bool? ThinkingEnabled = null,
        string? Effort = null,
        IReadOnlyDictionary<string, JsonElement>? ExtraSettings = null);

    public record LlmCompletionResult(string Text, int InputTokens, int OutputTokens, string Model, int LatencyMs);
}
