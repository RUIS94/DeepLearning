using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
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
            public Dictionary<string, LlmProviderSettings> ByProviderKey { get; } = new();

            public Task<LlmProviderSettings?> GetActiveAsync(CancellationToken cancellationToken = default) => Task.FromResult(Active);
            public Task<LlmProviderSettings?> GetByProviderKeyAsync(string providerKey, CancellationToken cancellationToken = default)
                => Task.FromResult(ByProviderKey.GetValueOrDefault(providerKey));
            public Task<List<LlmProviderSettings>> ListAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }

        private class StubModelRepository : ILlmProviderModelRepository
        {
            public LlmProviderModel? Current { get; set; }
            public Dictionary<string, LlmProviderModel> CurrentByProviderKey { get; } = new();

            public Task AddAsync(LlmProviderModel model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<LlmProviderModel?> GetByProviderKeyAndModelAsync(string providerKey, string model, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<LlmProviderModel?> GetCurrentAsync(string providerKey, CancellationToken cancellationToken = default)
                => Task.FromResult(CurrentByProviderKey.TryGetValue(providerKey, out var model) ? model : Current);
            public Task<List<LlmProviderModel>> ListByProviderKeyAsync(string providerKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<List<LlmProviderModel>> ListCurrentAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }

        private class StubOverrideRepository : IAiOperationProviderOverrideRepository
        {
            public Dictionary<AiOperationType, AiOperationProviderOverride> ByOperationType { get; } = new();

            public Task<AiOperationProviderOverride?> GetByOperationTypeAsync(AiOperationType operationType, CancellationToken cancellationToken = default)
                => Task.FromResult(ByOperationType.GetValueOrDefault(operationType));
            public Task<List<AiOperationProviderOverride>> ListAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task AddAsync(AiOperationProviderOverride entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public void Remove(AiOperationProviderOverride entity) => throw new NotSupportedException();
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

        private static (LlmClientResolver Resolver, StubSettingsRepository SettingsRepo, StubModelRepository ModelRepo, StubOverrideRepository OverrideRepo, RecordingLlmClient Client) Build(params string[] providerKeys)
        {
            var settingsRepo = new StubSettingsRepository();
            var modelRepo = new StubModelRepository();
            var overrideRepo = new StubOverrideRepository();
            var client = new RecordingLlmClient();
            var services = new ServiceCollection();
            foreach (var key in providerKeys)
            {
                services.AddKeyedSingleton<ILlmClient>(key, client);
            }
            var provider = services.BuildServiceProvider();

            return (new LlmClientResolver(settingsRepo, modelRepo, overrideRepo, provider, NullLogger<LlmClientResolver>.Instance), settingsRepo, modelRepo, overrideRepo, client);
        }

        [Fact]
        public async Task Falls_back_to_mimo_when_no_provider_is_active()
        {
            var (resolver, _, _, _, client) = Build(LlmClientResolver.FallbackProviderKey);

            var resolved = await resolver.GetActiveClientAsync(AiOperationType.grading);
            await resolved.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10));

            Assert.Same(client, resolved);
        }

        [Fact]
        public async Task Resolves_the_keyed_client_matching_the_active_providers_key()
        {
            var (resolver, settingsRepo, modelRepo, _, client) = Build("deepseek");
            settingsRepo.Active = new LlmProviderSettings { ProviderKey = "deepseek", IsActive = true };
            modelRepo.Current = new LlmProviderModel { ProviderKey = "deepseek", Model = "deepseek-v4-flash", IsCurrent = true };

            var resolved = await resolver.GetActiveClientAsync(AiOperationType.grading);
            await resolved.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10));

            Assert.Equal("deepseek-v4-flash", client.LastRequest!.Model);
        }

        [Fact]
        public async Task Fills_in_model_thinking_and_effort_from_the_active_settings_row_when_the_request_does_not_specify_them()
        {
            var (resolver, settingsRepo, modelRepo, _, client) = Build("claude");
            settingsRepo.Active = new LlmProviderSettings
            {
                ProviderKey = "claude",
                ThinkingEnabled = false,
                Effort = "xhigh",
            };
            modelRepo.Current = new LlmProviderModel { ProviderKey = "claude", Model = "claude-opus-5", IsCurrent = true };

            var resolved = await resolver.GetActiveClientAsync(AiOperationType.grading);
            await resolved.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10));

            Assert.Equal("claude-opus-5", client.LastRequest!.Model);
            Assert.False(client.LastRequest.ThinkingEnabled);
            Assert.Equal("xhigh", client.LastRequest.Effort);
        }

        [Fact]
        public async Task No_current_model_leaves_model_null_so_the_adapters_own_configured_default_wins()
        {
            var (resolver, settingsRepo, _, _, client) = Build("claude");
            settingsRepo.Active = new LlmProviderSettings { ProviderKey = "claude" };

            var resolved = await resolver.GetActiveClientAsync(AiOperationType.grading);
            await resolved.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10));

            Assert.Null(client.LastRequest!.Model);
        }

        [Fact]
        public async Task A_per_call_override_wins_over_the_active_settings_row()
        {
            var (resolver, settingsRepo, modelRepo, _, client) = Build("claude");
            settingsRepo.Active = new LlmProviderSettings { ProviderKey = "claude", Effort = "low" };
            modelRepo.Current = new LlmProviderModel { ProviderKey = "claude", Model = "claude-opus-5", IsCurrent = true };

            var resolved = await resolver.GetActiveClientAsync(AiOperationType.grading);
            await resolved.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10, Effort: "max"));

            Assert.Equal("max", client.LastRequest!.Effort);
        }

        [Fact]
        public async Task Parses_extra_settings_json_into_the_request()
        {
            var (resolver, settingsRepo, modelRepo, _, client) = Build("claude");
            settingsRepo.Active = new LlmProviderSettings
            {
                ProviderKey = "claude",
                ExtraSettings = "{\"reasoning_effort\":\"high\"}",
            };
            modelRepo.Current = new LlmProviderModel { ProviderKey = "claude", Model = "claude-opus-5", IsCurrent = true };

            var resolved = await resolver.GetActiveClientAsync(AiOperationType.grading);
            await resolved.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10));

            Assert.NotNull(client.LastRequest!.ExtraSettings);
            Assert.Equal("high", client.LastRequest.ExtraSettings!["reasoning_effort"].GetString());
        }

        [Fact]
        public async Task An_operation_override_wins_over_the_globally_active_provider()
        {
            var (resolver, settingsRepo, modelRepo, overrideRepo, client) = Build("mimo", "claude");
            settingsRepo.Active = new LlmProviderSettings { ProviderKey = "mimo", IsActive = true };
            settingsRepo.ByProviderKey["claude"] = new LlmProviderSettings { ProviderKey = "claude", Effort = "xhigh" };
            modelRepo.CurrentByProviderKey["claude"] = new LlmProviderModel { ProviderKey = "claude", Model = "claude-opus-5", IsCurrent = true };
            overrideRepo.ByOperationType[AiOperationType.grading] = new AiOperationProviderOverride
            {
                OperationType = AiOperationType.grading,
                ProviderKey = "claude",
            };

            // weak_point_classification has no override, so it still follows the global mimo default.
            var gradingClient = await resolver.GetActiveClientAsync(AiOperationType.grading);
            await gradingClient.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10));
            Assert.Equal("claude-opus-5", client.LastRequest!.Model);
            Assert.Equal("xhigh", client.LastRequest.Effort);
        }

        [Fact]
        public async Task A_dangling_override_falls_back_to_the_global_active_provider()
        {
            var (resolver, settingsRepo, modelRepo, overrideRepo, client) = Build("mimo");
            settingsRepo.Active = new LlmProviderSettings { ProviderKey = "mimo", IsActive = true };
            modelRepo.Current = new LlmProviderModel { ProviderKey = "mimo", Model = "mimo-v2.5-pro", IsCurrent = true };
            // Pinned to a provider whose llm_provider_settings row no longer exists.
            overrideRepo.ByOperationType[AiOperationType.grading] = new AiOperationProviderOverride
            {
                OperationType = AiOperationType.grading,
                ProviderKey = "deleted-provider",
            };

            var resolved = await resolver.GetActiveClientAsync(AiOperationType.grading);
            await resolved.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10));

            Assert.Equal("mimo-v2.5-pro", client.LastRequest!.Model);
        }

        [Fact]
        public async Task An_overrides_own_model_wins_over_the_pinned_providers_current_model()
        {
            var (resolver, settingsRepo, modelRepo, overrideRepo, client) = Build("claude");
            settingsRepo.ByProviderKey["claude"] = new LlmProviderSettings { ProviderKey = "claude" };
            modelRepo.CurrentByProviderKey["claude"] = new LlmProviderModel { ProviderKey = "claude", Model = "claude-opus-5", IsCurrent = true };
            overrideRepo.ByOperationType[AiOperationType.grading] = new AiOperationProviderOverride
            {
                OperationType = AiOperationType.grading,
                ProviderKey = "claude",
                Model = "claude-sonnet-5", // deliberately not the provider's IsCurrent model
            };

            var resolved = await resolver.GetActiveClientAsync(AiOperationType.grading);
            await resolved.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10));

            Assert.Equal("claude-sonnet-5", client.LastRequest!.Model);
        }

        [Fact]
        public async Task An_overrides_own_thinking_flag_wins_over_the_pinned_providers_own_default()
        {
            var (resolver, settingsRepo, _, overrideRepo, client) = Build("claude");
            settingsRepo.ByProviderKey["claude"] = new LlmProviderSettings { ProviderKey = "claude", ThinkingEnabled = true };
            overrideRepo.ByOperationType[AiOperationType.grading] = new AiOperationProviderOverride
            {
                OperationType = AiOperationType.grading,
                ProviderKey = "claude",
                ThinkingEnabled = false,
            };

            var resolved = await resolver.GetActiveClientAsync(AiOperationType.grading);
            await resolved.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10));

            Assert.False(client.LastRequest!.ThinkingEnabled);
        }

        [Fact]
        public async Task An_overrides_own_effort_wins_over_the_pinned_providers_own_default()
        {
            var (resolver, settingsRepo, _, overrideRepo, client) = Build("claude");
            settingsRepo.ByProviderKey["claude"] = new LlmProviderSettings { ProviderKey = "claude", Effort = "low" };
            overrideRepo.ByOperationType[AiOperationType.grading] = new AiOperationProviderOverride
            {
                OperationType = AiOperationType.grading,
                ProviderKey = "claude",
                Effort = "xhigh",
            };

            var resolved = await resolver.GetActiveClientAsync(AiOperationType.grading);
            await resolved.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10));

            Assert.Equal("xhigh", client.LastRequest!.Effort);
        }

        [Fact]
        public async Task An_override_with_no_model_thinking_or_effort_set_follows_the_pinned_providers_own_defaults()
        {
            var (resolver, settingsRepo, modelRepo, overrideRepo, client) = Build("claude");
            settingsRepo.ByProviderKey["claude"] = new LlmProviderSettings { ProviderKey = "claude", ThinkingEnabled = false, Effort = "medium" };
            modelRepo.CurrentByProviderKey["claude"] = new LlmProviderModel { ProviderKey = "claude", Model = "claude-opus-5", IsCurrent = true };
            overrideRepo.ByOperationType[AiOperationType.grading] = new AiOperationProviderOverride
            {
                OperationType = AiOperationType.grading,
                ProviderKey = "claude",
                // Model, ThinkingEnabled and Effort deliberately left null.
            };

            var resolved = await resolver.GetActiveClientAsync(AiOperationType.grading);
            await resolved.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10));

            Assert.Equal("claude-opus-5", client.LastRequest!.Model);
            Assert.False(client.LastRequest.ThinkingEnabled);
            Assert.Equal("medium", client.LastRequest.Effort);
        }
    }
}
