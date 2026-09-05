using System.Text.Json;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DeepLearning.Infrastructure.Ai
{
    public class LlmClientResolver : ILlmClientResolver
    {
        /// <summary>
        /// Used when llm_provider_settings has no is_active=true row (missing seed data, or
        /// the table hasn't been created against this environment's DB yet) — a broken/empty
        /// config table shouldn't hard-fail the whole AI feature. The fallback client still
        /// works correctly with no settings row: OpenAiCompatibleLlmClient/ClaudeLlmClient
        /// both fall back to their own appsettings-configured Model when the request doesn't
        /// specify one.
        /// </summary>
        public const string FallbackProviderKey = "mimo";

        private readonly ILlmProviderSettingsRepository _settingsRepository;
        private readonly ILlmProviderModelRepository _modelRepository;
        private readonly IAiOperationProviderOverrideRepository _overrideRepository;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LlmClientResolver> _logger;

        public LlmClientResolver(
            ILlmProviderSettingsRepository settingsRepository,
            ILlmProviderModelRepository modelRepository,
            IAiOperationProviderOverrideRepository overrideRepository,
            IServiceProvider serviceProvider,
            ILogger<LlmClientResolver> logger)
        {
            _settingsRepository = settingsRepository;
            _modelRepository = modelRepository;
            _overrideRepository = overrideRepository;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<ILlmClient> GetActiveClientAsync(AiOperationType operationType, CancellationToken cancellationToken = default)
        {
            var resolved = await ResolveAsync(operationType, cancellationToken);

            if (resolved is null)
            {
                _logger.LogWarning(
                    "No active LLM provider is configured (llm_provider_settings has no is_active=true row) — falling back to {FallbackProvider}.",
                    FallbackProviderKey);

                return _serviceProvider.GetRequiredKeyedService<ILlmClient>(FallbackProviderKey);
            }

            var innerClient = _serviceProvider.GetRequiredKeyedService<ILlmClient>(resolved.Value.Settings.ProviderKey);
            return new ConfiguredLlmClient(
                innerClient, resolved.Value.Settings, resolved.Value.Model, resolved.Value.ThinkingEnabled, resolved.Value.Effort);
        }

        private readonly record struct Resolved(LlmProviderSettings Settings, string? Model, bool ThinkingEnabled, string? Effort);

        /// <summary>
        /// A pinned provider for this operation type wins over the global default — it is looked
        /// up by ProviderKey directly (not GetActiveAsync), so the pinned provider need not be
        /// the one that is globally is_active=true. The override's own Model/ThinkingEnabled/Effort
        /// (if set) win over the pinned provider's own defaults too — e.g. grading can run Claude
        /// at effort=xhigh with thinking on while followup runs the same Claude at effort=low with
        /// it off. Missing/dangling override rows (the pinned provider's settings row was deleted)
        /// fall through to the global default rather than failing the call outright.
        /// </summary>
        private async Task<Resolved?> ResolveAsync(AiOperationType operationType, CancellationToken cancellationToken)
        {
            var overrideRow = await _overrideRepository.GetByOperationTypeAsync(operationType, cancellationToken);
            if (overrideRow is not null)
            {
                var pinnedSettings = await _settingsRepository.GetByProviderKeyAsync(overrideRow.ProviderKey, cancellationToken);
                if (pinnedSettings is not null)
                {
                    var model = overrideRow.Model
                        ?? (await _modelRepository.GetCurrentAsync(overrideRow.ProviderKey, cancellationToken))?.Model;
                    return new Resolved(
                        pinnedSettings,
                        model,
                        overrideRow.ThinkingEnabled ?? pinnedSettings.ThinkingEnabled,
                        overrideRow.Effort ?? pinnedSettings.Effort);
                }

                _logger.LogWarning(
                    "{OperationType} is pinned to provider {ProviderKey}, but that provider has no llm_provider_settings row — falling back to the global active provider.",
                    operationType, overrideRow.ProviderKey);
            }

            var activeSettings = await _settingsRepository.GetActiveAsync(cancellationToken);
            if (activeSettings is null)
            {
                return null;
            }

            var currentModel = await _modelRepository.GetCurrentAsync(activeSettings.ProviderKey, cancellationToken);
            return new Resolved(activeSettings, currentModel?.Model, activeSettings.ThinkingEnabled, activeSettings.Effort);
        }

        /// <summary>
        /// Decorates a keyed ILlmClient with the resolved Model/ThinkingEnabled/Effort and the
        /// settings row's ExtraSettings as defaults — callers may still override any of these
        /// per-call by setting them explicitly on the request.
        /// </summary>
        private class ConfiguredLlmClient : ILlmClient
        {
            private readonly ILlmClient _inner;
            private readonly LlmProviderSettings _settings;
            private readonly string? _model;
            private readonly bool _thinkingEnabled;
            private readonly string? _effort;

            public ConfiguredLlmClient(
                ILlmClient inner, LlmProviderSettings settings, string? model, bool thinkingEnabled, string? effort)
            {
                _inner = inner;
                _settings = settings;
                _model = model;
                _thinkingEnabled = thinkingEnabled;
                _effort = effort;
            }

            public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
            {
                var merged = request with
                {
                    Model = request.Model ?? _model,
                    ThinkingEnabled = request.ThinkingEnabled ?? _thinkingEnabled,
                    Effort = request.Effort ?? _effort,
                    ExtraSettings = request.ExtraSettings ?? ParseExtraSettings(_settings.ExtraSettings),
                };

                return _inner.CompleteAsync(merged, cancellationToken);
            }

            private static IReadOnlyDictionary<string, JsonElement>? ParseExtraSettings(string? json)
            {
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                using var document = JsonDocument.Parse(json);
                return document.RootElement.EnumerateObject()
                    .ToDictionary(p => p.Name, p => p.Value.Clone());
            }
        }
    }
}
