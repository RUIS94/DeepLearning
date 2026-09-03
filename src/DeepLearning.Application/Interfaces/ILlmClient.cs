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
        IReadOnlyDictionary<string, JsonElement>? ExtraSettings = null,
        // Per-call sampling temperature. Null = leave it to the provider default (~1.0 for the
        // OpenAI-compatible providers), which is what generation calls want. Deterministic
        // calls (grading, weak-point classification) pass 0 so the same input reproduces the
        // same output. Overrides any "temperature" in the provider's ExtraSettings. On Claude
        // it is only forwarded when ThinkingEnabled == false (Anthropic rejects temperature
        // != 1 while extended thinking is on).
        decimal? Temperature = null);

    public record LlmCompletionResult(string Text, int InputTokens, int OutputTokens, string Model, int LatencyMs);
}
