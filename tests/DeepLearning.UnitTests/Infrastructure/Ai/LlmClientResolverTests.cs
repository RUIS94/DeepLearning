using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Infrastructure.Ai;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

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

        private class StubModelRepository : ILlmProviderModelRepository
        {
            public LlmProviderModel? Current { get; set; }

            public Task AddAsync(LlmProviderModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<LlmProviderModel?> GetByProviderKeyAndModelAsync(string providerKey, string model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<LlmProviderModel?> GetCurrentAsync(string providerKey, CancellationToken cancellationToken = default) => Task.FromResult(Current);
            public Task<List<LlmProviderModel>> ListByProviderKeyAsync(string providerKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<List<LlmProviderModel>> ListCurrentAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
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

        private static (LlmClientResolver Resolver, StubSettingsRepository SettingsRepo, StubModelRepository ModelRepo, RecordingLlmClient Client) Build(params string[] providerKeys)
        {
            var settingsRepo = new StubSettingsRepository();
            var modelRepo = new StubModelRepository();
            var client = new RecordingLlmClient();
            var services = new ServiceCollection();
            foreach (var key in providerKeys)
            {
                services.AddKeyedSingleton<ILlmClient>(key, client);
            }
            var provider = services.BuildServiceProvider();

            return (new LlmClientResolver(settingsRepo, modelRepo, provider, NullLogger<LlmClientResolver>.Instance), settingsRepo, modelRepo, client);
        }

        [Fact]
        public async Task Falls_back_to_mimo_when_no_provider_is_active()
        {
            var (resolver, _, _, client) = Build(LlmClientResolver.FallbackProviderKey);

            var resolved = await resolver.GetActiveClientAsync();
            await resolved.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10));

            Assert.Same(client, resolved);
        }

        [Fact]
        public async Task Resolves_the_keyed_client_matching_the_active_providers_key()
        {
            var (resolver, settingsRepo, modelRepo, client) = Build("deepseek");
            settingsRepo.Active = new LlmProviderSettings { ProviderKey = "deepseek", IsActive = true };
            modelRepo.Current = new LlmProviderModel { ProviderKey = "deepseek", Model = "deepseek-v4-flash", IsCurrent = true };

            var resolved = await resolver.GetActiveClientAsync();
            await resolved.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10));

            Assert.Equal("deepseek-v4-flash", client.LastRequest!.Model);
        }

        [Fact]
        public async Task Fills_in_model_thinking_and_effort_from_the_active_settings_row_when_the_request_does_not_specify_them()
        {
            var (resolver, settingsRepo, modelRepo, client) = Build("claude");
            settingsRepo.Active = new LlmProviderSettings
            {
                ProviderKey = "claude",
                ThinkingEnabled = false,
                Effort = "xhigh",
            };
            modelRepo.Current = new LlmProviderModel { ProviderKey = "claude", Model = "claude-opus-5", IsCurrent = true };

            var resolved = await resolver.GetActiveClientAsync();
            await resolved.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10));

            Assert.Equal("claude-opus-5", client.LastRequest!.Model);
            Assert.False(client.LastRequest.ThinkingEnabled);
            Assert.Equal("xhigh", client.LastRequest.Effort);
        }

        [Fact]
        public async Task No_current_model_leaves_model_null_so_the_adapters_own_configured_default_wins()
        {
            var (resolver, settingsRepo, _, client) = Build("claude");
            settingsRepo.Active = new LlmProviderSettings { ProviderKey = "claude" };

            var resolved = await resolver.GetActiveClientAsync();
            await resolved.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10));

            Assert.Null(client.LastRequest!.Model);
        }

        [Fact]
        public async Task A_per_call_override_wins_over_the_active_settings_row()
        {
            var (resolver, settingsRepo, modelRepo, client) = Build("claude");
            settingsRepo.Active = new LlmProviderSettings { ProviderKey = "claude", Effort = "low" };
            modelRepo.Current = new LlmProviderModel { ProviderKey = "claude", Model = "claude-opus-5", IsCurrent = true };

            var resolved = await resolver.GetActiveClientAsync();
            await resolved.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10, Effort: "max"));

            Assert.Equal("max", client.LastRequest!.Effort);
        }

        [Fact]
        public async Task Parses_extra_settings_json_into_the_request()
        {
            var (resolver, settingsRepo, modelRepo, client) = Build("claude");
            settingsRepo.Active = new LlmProviderSettings
            {
                ProviderKey = "claude",
                ExtraSettings = "{\"reasoning_effort\":\"high\"}",
            };
            modelRepo.Current = new LlmProviderModel { ProviderKey = "claude", Model = "claude-opus-5", IsCurrent = true };

            var resolved = await resolver.GetActiveClientAsync();
            await resolved.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10));

            Assert.NotNull(client.LastRequest!.ExtraSettings);
            Assert.Equal("high", client.LastRequest.ExtraSettings!["reasoning_effort"].GetString());
        }
    }
}
