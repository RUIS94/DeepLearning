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

            // "Thinking" isn't a universal toggle across OpenAI-shaped providers — OpenAI's
            // older reasoning models use separate model ids entirely — so it is only sent for a
            // provider that has declared the field name it wants (see
            // OpenAiCompatibleOptions.ThinkingParameterName). Both Mimo and DeepSeek's current
            // v4 models call it "thinking", an object rather than a bool. Anything else still
            // goes through ExtraSettings below.
            if (_options.ThinkingParameterName is { Length: > 0 } thinkingField
                && request.ThinkingEnabled is { } thinkingEnabled)
            {
                body[thinkingField] = new { type = thinkingEnabled ? "enabled" : "disabled" };
            }

            // Reasoning strength once thinking is on — a separate knob from the on/off switch
            // above, and again only sent where the provider has declared a field name for it
            // (see OpenAiCompatibleOptions.ReasoningEffortFieldName). DeepSeek calls it
            // "reasoning_effort" and accepts the same low/medium/high/xhigh/max scale
            // LlmProviderSettings.Effort already uses for Claude, so this is a straight
            // passthrough — no remapping needed on our side.
            if (_options.ReasoningEffortFieldName is { Length: > 0 } effortField
                && !string.IsNullOrEmpty(request.Effort))
            {
                body[effortField] = request.Effort;
            }

            if (request.ExtraSettings is not null)
            {
                foreach (var (key, value) in request.ExtraSettings)
                {
                    body[key] = value;
                }
            }

            // Explicit per-call temperature wins over any ExtraSettings default — grading and
            // weak-point classification pass 0 so the same input reproduces the same output
            // (these providers otherwise sample at ~1.0). Generation calls leave it null.
            if (request.Temperature is { } temperature)
            {
                body["temperature"] = temperature;
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

            var choice = parsed.Choices?.FirstOrDefault();
            var text = choice?.Message?.Content ?? string.Empty;

            return new LlmCompletionResult(
                text,
                parsed.Usage?.PromptTokens ?? 0,
                parsed.Usage?.CompletionTokens ?? 0,
                parsed.Model ?? _options.Model,
                (int)stopwatch.ElapsedMilliseconds,
                // "length" is the OpenAI wire format's word for "I hit max_tokens and stopped
                // mid-sentence". Reading it is what separates "the model produced bad JSON"
                // from "the model was cut off", two failures with opposite fixes.
                Truncated: string.Equals(choice?.FinishReason, "length", StringComparison.OrdinalIgnoreCase));
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

            [JsonPropertyName("finish_reason")]
            public string? FinishReason { get; set; }
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
