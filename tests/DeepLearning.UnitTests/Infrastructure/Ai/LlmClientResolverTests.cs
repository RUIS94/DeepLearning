using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using DeepLearning.Infrastructure.Ai;
using Microsoft.Extensions.DependencyInjection;

namespace DeepLearning.UnitTests.Infrastructure.Ai
{
    public class LlmClientResolverTests
    {
        private class StubSettingsRepository : ILlmProviderSettingsRepository
        {
            public LlmProviderSettings? Active { get; set; }

            public Task<LlmProviderSettings?> GetActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult(Active);
            public Task<LlmProviderSettings?> GetByProviderKeyAsync(string providerKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<List<LlmProviderSettings>> ListAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }

        private class RecordingLlmClient : ILlmClient
        {
            public LlmCompletionRequest? LastRequest { get; private set; }

            public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
            {
                LastRequest = request;
                return Task.FromResult(new LlmCompletionResult("ok", 1, 1, "some-model", 1));
            }
        }

        private static (LlmClientResolver Resolver, StubSettingsRepository Repo, RecordingLlmClient Client) Build(string providerKey)
        {
            var repo = new StubSettingsRepository();
            var client = new RecordingLlmClient();
            var services = new ServiceCollection();
            services.AddKeyedSingleton<ILlmClient>(providerKey, client);
            var provider = services.BuildServiceProvider();

            return (new LlmClientResolver(repo, provider), repo, client);
        }

        [Fact]
        public async Task Throws_when_no_provider_is_active()
        {
            var (resolver, _, _) = Build("claude");

            await Assert.ThrowsAsync<AiCallFailedException>(() => resolver.GetActiveClientAsync());
        }

        [Fact]
        public async Task Resolves_the_keyed_client_matching_the_active_providers_key()
        {
            var (resolver, repo, client) = Build("deepseek");
            repo.Active = new LlmProviderSettings { ProviderKey = "deepseek", Model = "deepseek-v4-flash", IsActive = true };

            var resolved = await resolver.GetActiveClientAsync();
            await resolved.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10));

            Assert.Equal("deepseek-v4-flash", client.LastRequest!.Model);
        }

        [Fact]
        public async Task Fills_in_model_thinking_and_effort_from_the_active_settings_row_when_the_request_does_not_specify_them()
        {
            var (resolver, repo, client) = Build("claude");
            repo.Active = new LlmProviderSettings
            {
                ProviderKey = "claude",
                Model = "claude-opus-5",
                ThinkingEnabled = false,
                Effort = "xhigh",
            };

            var resolved = await resolver.GetActiveClientAsync();
            await resolved.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10));

            Assert.Equal("claude-opus-5", client.LastRequest!.Model);
            Assert.False(client.LastRequest.ThinkingEnabled);
            Assert.Equal("xhigh", client.LastRequest.Effort);
        }

        [Fact]
        public async Task A_per_call_override_wins_over_the_active_settings_row()
        {
            var (resolver, repo, client) = Build("claude");
            repo.Active = new LlmProviderSettings { ProviderKey = "claude", Model = "claude-opus-5", Effort = "low" };

            var resolved = await resolver.GetActiveClientAsync();
            await resolved.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10, Effort: "max"));

            Assert.Equal("max", client.LastRequest!.Effort);
        }

        [Fact]
        public async Task Parses_extra_settings_json_into_the_request()
        {
            var (resolver, repo, client) = Build("claude");
            repo.Active = new LlmProviderSettings
            {
                ProviderKey = "claude",
                Model = "claude-opus-5",
                ExtraSettings = "{\"reasoning_effort\":\"high\"}",
            };

            var resolved = await resolver.GetActiveClientAsync();
            await resolved.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10));

            Assert.NotNull(client.LastRequest!.ExtraSettings);
            Assert.Equal("high", client.LastRequest.ExtraSettings!["reasoning_effort"].GetString());
        }
    }
}
