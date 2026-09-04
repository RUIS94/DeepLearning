using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Exceptions;
using DeepLearning.Infrastructure.Ai;
using DeepLearning.Infrastructure.Ai.Options;

namespace DeepLearning.UnitTests.Infrastructure.Ai
{
    public class OpenAiCompatibleLlmClientTests
    {
        private class CapturingHandler : HttpMessageHandler
        {
            public HttpRequestMessage? CapturedRequest { get; private set; }
            public string? CapturedBody { get; private set; }
            public HttpResponseMessage ResponseToReturn { get; set; } = new(HttpStatusCode.OK);

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CapturedRequest = request;
                CapturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                return ResponseToReturn;
            }
        }

        private static HttpResponseMessage BuildSuccessResponse(string content) => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                choices = new[] { new { message = new { content } } },
                model = "the-model",
                usage = new { prompt_tokens = 12, completion_tokens = 34 },
            }),
        };

        [Fact]
        public async Task Sends_the_configured_auth_header_and_max_tokens_field_name()
        {
            var handler = new CapturingHandler { ResponseToReturn = BuildSuccessResponse("hello") };
            var httpClient = new HttpClient(handler);
            var options = new OpenAiCompatibleOptions
            {
                ApiKey = "test-key",
                BaseUrl = "https://example.test/v1/chat/completions",
                Model = "test-model",
                AuthHeaderName = "api-key",
                AuthHeaderValuePrefix = "",
                MaxTokensFieldName = "max_completion_tokens",
            };
            var client = new OpenAiCompatibleLlmClient(httpClient, options, "TestProvider");

            await client.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 100));

            Assert.Equal("test-key", handler.CapturedRequest!.Headers.GetValues("api-key").Single());
            Assert.Contains("\"max_completion_tokens\":100", handler.CapturedBody);
            Assert.DoesNotContain("\"max_tokens\"", handler.CapturedBody);
        }

        [Fact]
        public async Task Uses_bearer_prefix_and_max_tokens_field_name_for_a_deepseek_style_provider()
        {
            var handler = new CapturingHandler { ResponseToReturn = BuildSuccessResponse("hello") };
            var httpClient = new HttpClient(handler);
            var options = new OpenAiCompatibleOptions
            {
                ApiKey = "test-key",
                BaseUrl = "https://example.test/v1/chat/completions",
                Model = "test-model",
                AuthHeaderName = "Authorization",
                AuthHeaderValuePrefix = "Bearer ",
                MaxTokensFieldName = "max_tokens",
            };
            var client = new OpenAiCompatibleLlmClient(httpClient, options, "TestProvider");

            await client.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 100));

            Assert.Equal("Bearer test-key", handler.CapturedRequest!.Headers.GetValues("Authorization").Single());
            Assert.Contains("\"max_tokens\":100", handler.CapturedBody);
        }

        [Fact]
        public async Task Includes_a_system_message_only_when_a_system_prompt_is_given()
        {
            var handler = new CapturingHandler { ResponseToReturn = BuildSuccessResponse("hello") };
            var httpClient = new HttpClient(handler);
            var options = new OpenAiCompatibleOptions { ApiKey = "k", BaseUrl = "https://example.test/x", Model = "m" };
            var client = new OpenAiCompatibleLlmClient(httpClient, options, "TestProvider");

            await client.CompleteAsync(new LlmCompletionRequest(SystemPrompt: "be nice", UserPrompt: "hi", MaxTokens: 10));

            Assert.Contains("\"role\":\"system\"", handler.CapturedBody);
            Assert.Contains("be nice", handler.CapturedBody);
        }

        [Fact]
        public async Task Parses_the_generated_text_and_token_usage_from_the_response()
        {
            var handler = new CapturingHandler { ResponseToReturn = BuildSuccessResponse("the generated text") };
            var httpClient = new HttpClient(handler);
            var options = new OpenAiCompatibleOptions { ApiKey = "k", BaseUrl = "https://example.test/x", Model = "m" };
            var client = new OpenAiCompatibleLlmClient(httpClient, options, "TestProvider");

            var result = await client.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10));

            Assert.Equal("the generated text", result.Text);
            Assert.Equal(12, result.InputTokens);
            Assert.Equal(34, result.OutputTokens);
        }

        [Fact]
        public async Task Forwards_a_seed_from_the_provider_extra_settings_without_letting_it_shadow_an_explicit_temperature()
        {
            // Grading's reproducibility story ends here: the three-stage split removes the
            // coupled-reasoning and shifting-prompt causes of run-to-run drift, but a single
            // call is only as deterministic as the provider makes it. GradeSubmissionCommandHandler
            // passes ExtraSettings: null precisely so LlmClientResolver can merge
            // llm_provider_settings.extra_settings in — this pins that such a seed really does
            // reach the wire, and that the explicit Temperature: 0 still wins over an
            // extra_settings temperature rather than being overwritten by it.
            var handler = new CapturingHandler { ResponseToReturn = BuildSuccessResponse("hello") };
            var httpClient = new HttpClient(handler);
            var options = new OpenAiCompatibleOptions { ApiKey = "k", BaseUrl = "https://example.test/x", Model = "m" };
            var client = new OpenAiCompatibleLlmClient(httpClient, options, "TestProvider");

            using var extraJson = JsonDocument.Parse("{\"seed\":7,\"temperature\":0.9}");
            var extra = extraJson.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());

            await client.CompleteAsync(new LlmCompletionRequest(
                SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10, ExtraSettings: extra, Temperature: 0m));

            Assert.Contains("\"seed\":7", handler.CapturedBody);
            Assert.Contains("\"temperature\":0", handler.CapturedBody);
            Assert.DoesNotContain("\"temperature\":0.9", handler.CapturedBody);
        }

        [Fact]
        public async Task Throws_ai_call_failed_exception_on_a_non_success_status_code()
        {
            var handler = new CapturingHandler
            {
                ResponseToReturn = new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("{\"error\":\"bad key\"}"),
                },
            };
            var httpClient = new HttpClient(handler);
            var options = new OpenAiCompatibleOptions { ApiKey = "k", BaseUrl = "https://example.test/x", Model = "m" };
            var client = new OpenAiCompatibleLlmClient(httpClient, options, "TestProvider");

            await Assert.ThrowsAsync<AiCallFailedException>(
                () => client.CompleteAsync(new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10)));
        }

        private static HttpResponseMessage BuildResponse(string content, string finishReason) => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                choices = new[] { new { message = new { content }, finish_reason = finishReason } },
                model = "the-model",
                usage = new { prompt_tokens = 12, completion_tokens = 34 },
            }),
        };

        [Theory]
        [InlineData("length", true)]
        [InlineData("stop", false)]
        [InlineData(null, false)]
        public async Task Reports_whether_the_output_token_cap_stopped_the_model(string? finishReason, bool expected)
        {
            // Without this the caller sees only a half-written payload, and System.Text.Json
            // describes that as a problem with whichever field the cut landed in — a message
            // that reads like a bad value and sends the fix in the wrong direction.
            var handler = new CapturingHandler { ResponseToReturn = BuildResponse("{\"a\": ", finishReason!) };
            var httpClient = new HttpClient(handler);
            var options = new OpenAiCompatibleOptions { ApiKey = "k", BaseUrl = "https://example.test/x", Model = "m" };
            var client = new OpenAiCompatibleLlmClient(httpClient, options, "TestProvider");

            var result = await client.CompleteAsync(
                new LlmCompletionRequest(SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10));

            Assert.Equal(expected, result.Truncated);
        }

        [Theory]
        [InlineData(true, "enabled")]
        [InlineData(false, "disabled")]
        public async Task Sends_the_providers_thinking_switch_when_one_is_configured(bool thinkingEnabled, string expected)
        {
            var handler = new CapturingHandler { ResponseToReturn = BuildSuccessResponse("hello") };
            var httpClient = new HttpClient(handler);
            var options = new OpenAiCompatibleOptions
            {
                ApiKey = "k",
                BaseUrl = "https://example.test/x",
                Model = "m",
                ThinkingParameterName = "thinking",
            };
            var client = new OpenAiCompatibleLlmClient(httpClient, options, "Mimo");

            await client.CompleteAsync(new LlmCompletionRequest(
                SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10, ThinkingEnabled: thinkingEnabled));

            Assert.Contains($"\"thinking\":{{\"type\":\"{expected}\"}}", handler.CapturedBody);
        }

        [Fact]
        public async Task Sends_no_thinking_switch_for_a_provider_that_declares_none()
        {
            // OpenAI selects reasoning by model id and DeepSeek by model name; sending them a
            // "thinking" object would be inventing a field their API never documented.
            var handler = new CapturingHandler { ResponseToReturn = BuildSuccessResponse("hello") };
            var httpClient = new HttpClient(handler);
            var options = new OpenAiCompatibleOptions { ApiKey = "k", BaseUrl = "https://example.test/x", Model = "m" };
            var client = new OpenAiCompatibleLlmClient(httpClient, options, "TestProvider");

            await client.CompleteAsync(new LlmCompletionRequest(
                SystemPrompt: null, UserPrompt: "hi", MaxTokens: 10, ThinkingEnabled: false));

            Assert.DoesNotContain("thinking", handler.CapturedBody);
        }
    }
}
