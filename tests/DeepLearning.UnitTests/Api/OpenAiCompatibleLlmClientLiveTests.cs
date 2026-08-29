using DeepLearning.Application.Interfaces;
using DeepLearning.UnitTests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace DeepLearning.UnitTests.Api
{
    /// <summary>
    /// Real, live calls to OpenAI/DeepSeek/Mimo — same treatment as ClaudeLlmClientLiveTests:
    /// excluded from the default run (`dotnet test --filter "Category!=LlmIntegration"`),
    /// resolves each provider directly by its DI key (bypassing Llm:Provider) so all three
    /// can be checked without flipping config between runs. Requires real keys under
    /// Llm:{OpenAi,DeepSeek,Mimo} in appsettings.Development.json.
    /// </summary>
    [Collection(ApiCollection.Name)]
    public class OpenAiCompatibleLlmClientLiveTests
    {
        private readonly ApiWebApplicationFactory _factory;

        public OpenAiCompatibleLlmClientLiveTests(ApiWebApplicationFactory factory)
        {
            _factory = factory;
        }

        private async Task AssertPongAsync(string providerKey)
        {
            using var scope = _factory.Services.CreateScope();
            var llmClient = scope.ServiceProvider.GetRequiredKeyedService<ILlmClient>(providerKey);

            var result = await llmClient.CompleteAsync(new LlmCompletionRequest(
                SystemPrompt: null,
                UserPrompt: "Reply with exactly the single word: PONG",
                MaxTokens: 32));

            Assert.False(string.IsNullOrWhiteSpace(result.Text));
            Assert.Contains("PONG", result.Text, StringComparison.OrdinalIgnoreCase);
        }

        [Trait("Category", "LlmIntegration")]
        [Fact]
        public Task OpenAI_returns_a_real_response() => AssertPongAsync("openai");

        [Trait("Category", "LlmIntegration")]
        [Fact]
        public Task DeepSeek_returns_a_real_response() => AssertPongAsync("deepseek");

        [Trait("Category", "LlmIntegration")]
        [Fact]
        public Task Mimo_returns_a_real_response() => AssertPongAsync("mimo");
    }
}
