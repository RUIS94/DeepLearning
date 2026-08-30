using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DeepLearning.Infrastructure.Ai
{
    /// <summary>
    /// Development-only diagnostic DelegatingHandler attached to every LLM provider's HttpClient
    /// pipeline (Claude + the shared OpenAI-compatible client — see DependencyInjection.cs).
    /// Registered AFTER AddStandardResilienceHandler in the fluent chain, which makes it the
    /// INNER handler (closer to the network) — Polly re-invokes everything inside it on each
    /// retry, so the call counter and log entries below reflect real outbound HTTP attempts,
    /// including retries, not just one entry per logical ILlmClient.CompleteAsync call.
    ///
    /// Logs the raw request/response JSON bodies verbatim. This means Claude's extended-thinking
    /// content comes along for free when thinking is enabled — ClaudeLlmClient's own response
    /// parsing discards thinking blocks and keeps only the final text, but that parsing happens
    /// after this handler has already seen (and logged) the untouched raw body, so no separate
    /// thinking-capture logic is needed here.
    ///
    /// Deliberately NEVER logs request/response headers — Claude's x-api-key and the OpenAI-
    /// compatible clients' Authorization/api-key headers carry real secrets, and headers add
    /// nothing a developer needs for reading prompt/response content.
    /// </summary>
    public class AiTracingHandler : DelegatingHandler
    {
        private static long _callSequence;

        private readonly ILogger<AiTracingHandler> _logger;
        private readonly IHostEnvironment _environment;

        public AiTracingHandler(ILogger<AiTracingHandler> logger, IHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!_environment.IsDevelopment())
            {
                return await base.SendAsync(request, cancellationToken);
            }

            var callNumber = Interlocked.Increment(ref _callSequence);

            // LoadIntoBufferAsync() first, then read — without it, HttpContent built on a
            // one-shot stream would be exhausted by our own read, leaving nothing for
            // ClaudeLlmClient/OpenAiCompatibleLlmClient's own subsequent ReadAsStringAsync call.
            // Once buffered, HttpContent supports being read any number of times.
            string? requestBody = null;
            if (request.Content is not null)
            {
                await request.Content.LoadIntoBufferAsync();
                requestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            _logger.LogInformation(
                "AI call #{CallNumber} -> {Method} {Uri}{NewLine}Request body:{NewLine}{RequestBody}",
                callNumber, request.Method, request.RequestUri, Environment.NewLine, Environment.NewLine, requestBody);

            var stopwatch = Stopwatch.StartNew();
            HttpResponseMessage response;
            try
            {
                response = await base.SendAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogWarning(ex, "AI call #{CallNumber} threw after {ElapsedMs}ms", callNumber, stopwatch.ElapsedMilliseconds);
                throw;
            }

            stopwatch.Stop();

            string? responseBody = null;
            if (response.Content is not null)
            {
                await response.Content.LoadIntoBufferAsync();
                responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            }

            _logger.LogInformation(
                "AI call #{CallNumber} <- {StatusCode} in {ElapsedMs}ms{NewLine}Response body:{NewLine}{ResponseBody}",
                callNumber, (int)response.StatusCode, stopwatch.ElapsedMilliseconds, Environment.NewLine, Environment.NewLine, responseBody);

            return response;
        }
    }
}
