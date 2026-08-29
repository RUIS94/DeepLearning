using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Exceptions;
using DeepLearning.Infrastructure.Ai.Options;
using Polly.Timeout;

namespace DeepLearning.Infrastructure.Ai
{
    /// <summary>
    /// One adapter shared by every OpenAI-Chat-Completions-shaped provider (OpenAI, DeepSeek,
    /// Mimo — confirmed identical response shape and near-identical request shape against each
    /// provider's own docs on 2026-08-29). Per-provider differences (base URL, auth header
    /// style, model, the output-length field name) live entirely in <see cref="OpenAiCompatibleOptions"/>,
    /// bound under a different config section per provider and injected as a different
    /// instance per keyed registration — see DependencyInjection.cs. Adding a fifth
    /// OpenAI-shaped provider needs zero new C#, just another named options section + keyed
    /// registration line.
    /// </summary>
    public class OpenAiCompatibleLlmClient : ILlmClient
    {
        private readonly HttpClient _httpClient;
        private readonly OpenAiCompatibleOptions _options;
        private readonly string _providerName;

        public OpenAiCompatibleLlmClient(HttpClient httpClient, OpenAiCompatibleOptions options, string providerName)
        {
            _httpClient = httpClient;
            _options = options;
            _providerName = providerName;
        }

        public async Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
        {
            var messages = new List<object>();
            if (!string.IsNullOrEmpty(request.SystemPrompt))
            {
                messages.Add(new { role = "system", content = request.SystemPrompt });
            }
            messages.Add(new { role = "user", content = request.UserPrompt });

            var body = new Dictionary<string, object?>
            {
                ["model"] = request.Model ?? _options.Model,
                ["messages"] = messages,
                [_options.MaxTokensFieldName] = request.MaxTokens,
            };

            // "Thinking" isn't a universal boolean toggle across OpenAI-shaped providers —
            // OpenAI's reasoning models use separate model ids or a Responses-API-only
            // reasoning_effort field, DeepSeek's reasoning is a distinct model name
            // ("deepseek-reasoner"), and Mimo's equivalent isn't confirmed. Rather than guess
            // a field name, provider-specific reasoning controls go through ExtraSettings
            // below (e.g. {"reasoning_effort":"high"}) once each provider's actual mechanism
            // is confirmed against its docs — ThinkingEnabled/Effort are intentionally unused
            // here (Claude-specific, see ClaudeLlmClient).
            if (request.ExtraSettings is not null)
            {
                foreach (var (key, value) in request.ExtraSettings)
                {
                    body[key] = value;
                }
            }

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.BaseUrl)
            {
                Content = JsonContent.Create(body),
            };
            httpRequest.Headers.Add(_options.AuthHeaderName, _options.AuthHeaderValuePrefix + _options.ApiKey);

            var stopwatch = Stopwatch.StartNew();
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TimeoutRejectedException)
            {
                throw new AiCallFailedException($"{_providerName} request failed: {ex.Message}", ex);
            }
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new AiCallFailedException(
                    $"{_providerName} request failed with status {(int)response.StatusCode} ({response.StatusCode}): {errorBody}");
            }

            var parsed = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken)
                ?? throw new AiCallFailedException($"{_providerName} returned an empty response body.");

            var text = parsed.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;

            return new LlmCompletionResult(
                text,
                parsed.Usage?.PromptTokens ?? 0,
                parsed.Usage?.CompletionTokens ?? 0,
                parsed.Model ?? _options.Model,
                (int)stopwatch.ElapsedMilliseconds);
        }

        private class ChatCompletionResponse
        {
            [JsonPropertyName("choices")]
            public List<ChatChoice>? Choices { get; set; }

            [JsonPropertyName("model")]
            public string? Model { get; set; }

            [JsonPropertyName("usage")]
            public ChatUsage? Usage { get; set; }
        }

        private class ChatChoice
        {
            [JsonPropertyName("message")]
            public ChatMessage? Message { get; set; }
        }

        private class ChatMessage
        {
            [JsonPropertyName("content")]
            public string? Content { get; set; }
        }

        private class ChatUsage
        {
            [JsonPropertyName("prompt_tokens")]
            public int PromptTokens { get; set; }

            [JsonPropertyName("completion_tokens")]
            public int CompletionTokens { get; set; }
        }
    }
}
