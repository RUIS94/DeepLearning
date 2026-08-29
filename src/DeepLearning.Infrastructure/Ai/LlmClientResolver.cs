using System.Text.Json;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace DeepLearning.Infrastructure.Ai
{
    public class LlmClientResolver : ILlmClientResolver
    {
        private readonly ILlmProviderSettingsRepository _settingsRepository;
        private readonly IServiceProvider _serviceProvider;

        public LlmClientResolver(ILlmProviderSettingsRepository settingsRepository, IServiceProvider serviceProvider)
        {
            _settingsRepository = settingsRepository;
            _serviceProvider = serviceProvider;
        }

        public async Task<ILlmClient> GetActiveClientAsync(CancellationToken cancellationToken = default)
        {
            var settings = await _settingsRepository.GetActiveAsync(cancellationToken)
                ?? throw new AiCallFailedException(
                    "No active LLM provider is configured (llm_provider_settings has no is_active=true row).");

            var innerClient = _serviceProvider.GetRequiredKeyedService<ILlmClient>(settings.ProviderKey);
            return new ConfiguredLlmClient(innerClient, settings);
        }

        /// <summary>
        /// Decorates a keyed ILlmClient with the active LlmProviderSettings row's
        /// Model/ThinkingEnabled/Effort/ExtraSettings as defaults — callers may still
        /// override any of these per-call by setting them explicitly on the request.
        /// </summary>
        private class ConfiguredLlmClient : ILlmClient
        {
            private readonly ILlmClient _inner;
            private readonly LlmProviderSettings _settings;

            public ConfiguredLlmClient(ILlmClient inner, LlmProviderSettings settings)
            {
                _inner = inner;
                _settings = settings;
            }

            public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
            {
                var merged = request with
                {
                    Model = request.Model ?? _settings.Model,
                    ThinkingEnabled = request.ThinkingEnabled ?? _settings.ThinkingEnabled,
                    Effort = request.Effort ?? _settings.Effort,
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
