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

    /// <param name="Truncated">
    /// The provider stopped because the output-token cap was reached, not because the model
    /// had finished. Each adapter maps its own vocabulary onto this flag (OpenAI-compatible
    /// finish_reason "length", Claude stop_reason "max_tokens") so callers never have to match
    /// provider dialects.
    ///
    /// <para>It matters because a truncated response is not a malformed one: the JSON simply
    /// stops mid-token, and System.Text.Json reports that as "Expected end of string, but
    /// instead reached end of data. Path: $.findings[9].errorCategory" — an error that names a
    /// field and reads exactly like a bad value in it. That message sent a real investigation
    /// after the wrong bug, and the retry it triggered told the model to check its
    /// errorCategory values, which was not the problem.</para>
    /// </param>
    public record LlmCompletionResult(
        string Text, int InputTokens, int OutputTokens, string Model, int LatencyMs, bool Truncated = false);
}
