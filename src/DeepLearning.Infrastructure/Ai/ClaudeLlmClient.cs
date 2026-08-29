using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Exceptions;
using DeepLearning.Infrastructure.Ai.Options;
using Microsoft.Extensions.Options;
using Polly.Timeout;

namespace DeepLearning.Infrastructure.Ai
{
    /// <summary>
    /// Claude adapter for ILlmClient: raw HttpClient call to POST /v1/messages (deliberately
    /// not the official Anthropic SDK — a uniform raw-HTTP shape is what makes adding another
    /// provider later a same-shaped adapter class, and the retry/circuit-breaker pipeline
    /// registered in DependencyInjection.cs attaches to HttpClient, not to an SDK client).
    /// Registered as a keyed service ("claude") — see DependencyInjection.AddInfrastructure.
    /// Model/ThinkingEnabled/Effort/ExtraSettings on the request come from the database via
    /// ILlmClientResolver, not from ClaudeApiOptions — _options.Model is only the fallback
    /// used when a caller builds a request directly (e.g. the live tests).
    /// </summary>
    public class ClaudeLlmClient : ILlmClient
    {
        private readonly HttpClient _httpClient;
        private readonly ClaudeApiOptions _options;

        public ClaudeLlmClient(HttpClient httpClient, IOptions<ClaudeApiOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;
        }

        public async Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
        {
            var body = new Dictionary<string, object?>
            {
                ["model"] = request.Model ?? _options.Model,
                ["max_tokens"] = request.MaxTokens,
                ["messages"] = new[] { new { role = "user", content = request.UserPrompt } },
            };

            if (!string.IsNullOrEmpty(request.SystemPrompt))
            {
                body["system"] = request.SystemPrompt;
            }

            // Opus 5 runs adaptive thinking by default when the param is omitted entirely;
            // only send it to explicitly disable (accepted at effort <= high — see the
            // Claude API skill's disabled-thinking caveats before relying on this at xhigh/max).
            if (request.ThinkingEnabled == false)
            {
                body["thinking"] = new { type = "disabled" };
            }

            if (!string.IsNullOrEmpty(request.Effort))
            {
                body["output_config"] = new { effort = request.Effort };
            }

            if (request.ExtraSettings is not null)
            {
                foreach (var (key, value) in request.ExtraSettings)
                {
                    body[key] = value;
                }
            }

            var stopwatch = Stopwatch.StartNew();
            HttpResponseMessage response;
            try
            {
                response = await _httpClient.PostAsJsonAsync("/v1/messages", body, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TimeoutRejectedException)
            {
                // TimeoutRejectedException: the resilience pipeline's per-attempt/total
                // timeout fired (real requirement — a question-generation call with
                // adaptive thinking genuinely took long enough to hit the library's
                // default 10s/30s timeouts against the real Claude API on 2026-08-29).
                throw new AiCallFailedException($"Claude request failed: {ex.Message}", ex);
            }
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new AiCallFailedException(
                    $"Claude request failed with status {(int)response.StatusCode} ({response.StatusCode}): {errorBody}");
            }

            var parsed = await response.Content.ReadFromJsonAsync<ClaudeMessageResponse>(cancellationToken)
                ?? throw new AiCallFailedException("Claude returned an empty response body.");

            var text = string.Concat(
                (parsed.Content ?? [])
                    .Where(block => block.Type == "text" && block.Text is not null)
                    .Select(block => block.Text));

            return new LlmCompletionResult(
                text,
                parsed.Usage?.InputTokens ?? 0,
                parsed.Usage?.OutputTokens ?? 0,
                parsed.Model ?? request.Model ?? _options.Model,
                (int)stopwatch.ElapsedMilliseconds);
        }

        private class ClaudeMessageResponse
        {
            [JsonPropertyName("content")]
            public List<ClaudeContentBlock>? Content { get; set; }

            [JsonPropertyName("model")]
            public string? Model { get; set; }

            [JsonPropertyName("usage")]
            public ClaudeUsage? Usage { get; set; }
        }

        private class ClaudeContentBlock
        {
            [JsonPropertyName("type")]
            public string Type { get; set; } = string.Empty;

            [JsonPropertyName("text")]
            public string? Text { get; set; }
        }

        private class ClaudeUsage
        {
            [JsonPropertyName("input_tokens")]
            public int InputTokens { get; set; }

            [JsonPropertyName("output_tokens")]
            public int OutputTokens { get; set; }
        }
    }
}
