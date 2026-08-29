namespace DeepLearning.Application.Interfaces
{
    /// <summary>
    /// Provider-neutral abstraction over "call a large language model and get text back".
    /// Concrete adapters (Claude, and later others) live in Infrastructure/Ai and are
    /// selected via keyed DI + the Llm:Provider config setting — this interface and every
    /// caller of it stay completely unaware of which provider is active.
    /// </summary>
    public interface ILlmClient
    {
        Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default);
    }

    public record LlmCompletionRequest(string? SystemPrompt, string UserPrompt, int MaxTokens);

    public record LlmCompletionResult(string Text, int InputTokens, int OutputTokens, string Model, int LatencyMs);
}
