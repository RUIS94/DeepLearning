using System.Net;
using DeepLearning.Infrastructure.Ai;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeepLearning.UnitTests.Infrastructure.Ai
{
    /// <summary>
    /// The one correctness property that matters most here: AiTracingHandler reads both the
    /// request and response bodies for logging, and MUST NOT exhaust them in the process —
    /// ClaudeLlmClient/OpenAiCompatibleLlmClient read the response body themselves right after
    /// this handler runs, so if LoadIntoBufferAsync() were missing or misused, every real AI call
    /// would come back with an empty response the moment tracing is enabled.
    ///
    /// The transcript-file side effect is pointed at a throwaway temp dir via the AI_TRACE_DIR
    /// override so the tests never write into the test host's working directory.
    /// </summary>
    public class AiTracingHandlerTests : IDisposable
    {
        private readonly string _traceDir;

        public AiTracingHandlerTests()
        {
            _traceDir = Path.Combine(Path.GetTempPath(), "ai-trace-tests", Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("AI_TRACE_DIR", _traceDir);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable("AI_TRACE_DIR", null);
            try
            {
                if (Directory.Exists(_traceDir))
                {
                    Directory.Delete(_traceDir, recursive: true);
                }
            }
            catch (IOException)
            {
                // best effort — a leaked temp dir is harmless
            }
        }

        private class FakeEnvironment : IHostEnvironment
        {
            public string EnvironmentName { get; set; } = Environments.Production;
            public string ApplicationName { get; set; } = "Test";
            public string ContentRootPath { get; set; } = ".";
            public IFileProvider ContentRootFileProvider { get; set; } = null!;
        }

        private class FakeInnerHandler : HttpMessageHandler
        {
            private readonly string _responseBody;

            public FakeInnerHandler(string responseBody)
            {
                _responseBody = responseBody;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_responseBody),
                });
        }

        private static HttpClient CreateClient(string environmentName, string responseBody)
        {
            var env = new FakeEnvironment { EnvironmentName = environmentName };
            var handler = new AiTracingHandler(NullLogger<AiTracingHandler>.Instance, env)
            {
                InnerHandler = new FakeInnerHandler(responseBody),
            };
            return new HttpClient(handler);
        }

        [Fact]
        public async Task Passes_the_response_through_untouched_when_not_development()
        {
            using var client = CreateClient(Environments.Production, "{\"result\":\"ok\"}");

            var response = await client.PostAsync("https://example.test/v1/messages", new StringContent("{\"prompt\":\"hi\"}"));
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal("{\"result\":\"ok\"}", body);
        }

        [Fact]
        public async Task Response_body_is_still_readable_by_the_caller_after_tracing_in_development()
        {
            using var client = CreateClient(Environments.Development, "{\"result\":\"ok\"}");

            var response = await client.PostAsync("https://example.test/v1/messages", new StringContent("{\"prompt\":\"hi\"}"));
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal("{\"result\":\"ok\"}", body);
        }

        [Fact]
        public async Task Works_for_a_content_less_get_request_in_development()
        {
            using var client = CreateClient(Environments.Development, "{\"result\":\"ok\"}");

            var response = await client.GetAsync("https://example.test/v1/models");
            var body = await response.Content.ReadAsStringAsync();

            Assert.Equal("{\"result\":\"ok\"}", body);
        }

        [Fact]
        public async Task Writes_a_decoded_transcript_file_in_development()
        {
            using var client = CreateClient(Environments.Development, "{\"choices\":[{\"message\":{\"content\":\"回复内容\"}}]}");

            await client.PostAsync(
                "https://example.test/v1/chat/completions",
                new StringContent("{\"model\":\"mimo-v2.5-pro\",\"messages\":[{\"role\":\"user\",\"content\":\"\\u4f60\\u597d\"}]}"));

            var file = Assert.Single(Directory.GetFiles(_traceDir));
            var text = await File.ReadAllTextAsync(file);

            Assert.Contains("### USER", text);
            Assert.Contains("你好", text);            // decoded, not 你好
            Assert.Contains("### ASSISTANT", text);
            Assert.Contains("回复内容", text);
            Assert.Contains("RAW REQUEST JSON", text);
        }

        [Fact]
        public async Task Does_not_write_a_transcript_file_when_not_development()
        {
            using var client = CreateClient(Environments.Production, "{\"result\":\"ok\"}");

            await client.PostAsync("https://example.test/v1/messages", new StringContent("{\"prompt\":\"hi\"}"));

            Assert.False(Directory.Exists(_traceDir));
        }
    }
}
