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
    /// </summary>
    public class AiTracingHandlerTests
    {
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
    }
}
