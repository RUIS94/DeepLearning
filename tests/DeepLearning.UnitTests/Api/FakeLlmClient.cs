using DeepLearning.Application.Interfaces;

namespace DeepLearning.UnitTests.Api
{
    /// <summary>
    /// Fixed-JSON stand-in for ILlmClient, swapped in via
    /// ApiWebApplicationFactory.WithWebHostBuilder(...) for tests that need to prove the
    /// GenerateQuestion pipeline (parse -> persist -> respond) without a real LLM call.
    /// </summary>
    public class FakeLlmClient : ILlmClient
    {
        public const string FixedTitle = "Fake Generated Question";
        public const string FixedSourceText = "This is a fake generated source text for testing.";

        public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
        {
            var json = $$"""
                {
                  "title": "{{FixedTitle}}",
                  "sourceText": "{{FixedSourceText}}",
                  "brief": {"domain": "test", "textType": "article"},
                  "wordCount": 42,
                  "meaningCheckpoints": [
                    {"checkpointText": "Must convey the fake fact.", "checkpointType": null, "importance": "core"}
                  ]
                }
                """;

            return Task.FromResult(new LlmCompletionResult(json, 10, 20, "fake-model", 5));
        }
    }

    /// <summary>
    /// Hands back FakeLlmClient directly, bypassing the real llm_provider_settings DB lookup —
    /// swapped in for ILlmClientResolver the same way FakeLlmClient is swapped in for ILlmClient.
    /// </summary>
    public class FakeLlmClientResolver : ILlmClientResolver
    {
        public Task<ILlmClient> GetActiveClientAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<ILlmClient>(new FakeLlmClient());
    }
}
