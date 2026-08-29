using DeepLearning.Application.Interfaces;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DeepLearning.UnitTests.Api
{
    /// <summary>
    /// Real, live call to the Claude API — excluded from the default test run
    /// (`dotnet test --filter "Category!=LlmIntegration"`, see AGENTS.md) per the design
    /// doc's own test strategy: these are meant to run explicitly / in a nightly job, not
    /// on every `dotnet test`, since they cost real money and depend on network + a live key.
    /// Requires a real Claude API key in the Llm__Claude__ApiKey environment variable
    /// (secrets moved out of appsettings — see AGENTS.md's "AI integration" section).
    /// Resolves the "claude" keyed service directly, bypassing ILlmClientResolver's
    /// database lookup — this test is about the adapter, not the DB-driven provider switch.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class ClaudeLlmClientLiveTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public ClaudeLlmClientLiveTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Trait("Category", "LlmIntegration")]
        [Fact]
        public async Task CompleteAsync_returns_a_real_response_from_claude()
        {
            using var scope = _factory.Services.CreateScope();
            var llmClient = scope.ServiceProvider.GetRequiredKeyedService<ILlmClient>("claude");

            var result = await llmClient.CompleteAsync(new LlmCompletionRequest(
                SystemPrompt: null,
                UserPrompt: "Reply with exactly the single word: PONG",
                MaxTokens: 32));

            Assert.False(string.IsNullOrWhiteSpace(result.Text));
            Assert.Contains("PONG", result.Text, StringComparison.OrdinalIgnoreCase);
        }
    }
}
