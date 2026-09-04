using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeepLearning.Application.Interfaces;
using DeepLearning.Infrastructure.Ai;
using DeepLearning.Infrastructure.Ai.Options;
using Microsoft.Extensions.Options;

namespace DeepLearning.UnitTests.Infrastructure.Ai
{
    public class ClaudeLlmClientTests
    {
        private class CapturingHandler : HttpMessageHandler
        {
            public string? CapturedBody { get; private set; }
            public HttpResponseMessage ResponseToReturn { get; set; } = new(HttpStatusCode.OK);

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CapturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                return ResponseToReturn;
            }
        }

        private static HttpResponseMessage BuildSuccessResponse() => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                content = new[] { new { type = "text", text = "hello" } },
                model = "claude-opus-5",
                usage = new { input_tokens = 1, output_tokens = 2 },
            }),
        };

        private static ClaudeLlmClient BuildClient(CapturingHandler handler) =>
            new(
                new HttpClient(handler) { BaseAddress = new Uri("https://example.test") },
                Options.Create(new ClaudeApiOptions { Model = "default-model" }));

        [Fact]
        public async Task Omits_the_thinking_field_when_thinking_enabled_is_not_explicitly_false()
        {
            var handler = new CapturingHandler { ResponseToReturn = BuildSuccessResponse() };
            var client = BuildClient(handler);

            await client.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10));

            Assert.DoesNotContain("\"thinking\"", handler.CapturedBody);
        }

        [Fact]
        public async Task Sends_thinking_disabled_when_thinking_enabled_is_explicitly_false()
        {
            var handler = new CapturingHandler { ResponseToReturn = BuildSuccessResponse() };
            var client = BuildClient(handler);

            await client.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10, ThinkingEnabled: false));

            Assert.Contains("\"thinking\":{\"type\":\"disabled\"}", handler.CapturedBody);
        }

        [Fact]
        public async Task Sends_output_config_effort_when_effort_is_set()
        {
            var handler = new CapturingHandler { ResponseToReturn = BuildSuccessResponse() };
            var client = BuildClient(handler);

            await client.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10, Effort: "xhigh"));

            Assert.Contains("\"output_config\":{\"effort\":\"xhigh\"}", handler.CapturedBody);
        }

        [Fact]
        public async Task Uses_the_requests_model_override_instead_of_the_configured_default()
        {
            var handler = new CapturingHandler { ResponseToReturn = BuildSuccessResponse() };
            var client = BuildClient(handler);

            await client.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10, Model: "claude-sonnet-5"));

            Assert.Contains("\"model\":\"claude-sonnet-5\"", handler.CapturedBody);
        }

        [Fact]
        public async Task Merges_extra_settings_into_the_top_level_request_body()
        {
            var handler = new CapturingHandler { ResponseToReturn = BuildSuccessResponse() };
            var client = BuildClient(handler);
            using var document = JsonDocument.Parse("{\"temperature\":0.5}");
            var extra = document.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());

            await client.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10, ExtraSettings: extra));

            Assert.Contains("\"temperature\":0.5", handler.CapturedBody);
        }

        [Theory]
        [InlineData("max_tokens", true)]
        [InlineData("end_turn", false)]
        [InlineData(null, false)]
        public async Task Reports_whether_the_output_token_cap_stopped_the_model(string? stopReason, bool expected)
        {
            // Anthropic's name for what the OpenAI-shaped providers call finish_reason "length".
            // Callers get one flag so they never have to match provider dialects.
            var handler = new CapturingHandler
            {
                ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        content = new[] { new { type = "text", text = "{\"a\": " } },
                        model = "claude-opus-5",
                        usage = new { input_tokens = 1, output_tokens = 2 },
                        stop_reason = stopReason,
                    }),
                },
            };
            var client = BuildClient(handler);

            var result = await client.CompleteAsync(
                new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10));

            Assert.Equal(expected, result.Truncated);
        }
    }
}
