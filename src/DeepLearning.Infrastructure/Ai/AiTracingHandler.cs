using System.Diagnostics;
using System.Text;
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
    /// Two outputs per call, both meant to be read by a human debugging prompts:
    ///  1. A console line (ILogger) with a DECODED transcript — AiTraceRenderer turns the raw
    ///     request/response JSON into "### SYSTEM / ### USER / ### ASSISTANT" sections with
    ///     literal CJK and real line breaks, instead of the \uXXXX-escaped single-line JSON.
    ///  2. One file per call under {ContentRoot}/logs/ai-trace/ (override with the AI_TRACE_DIR
    ///     env var) holding the same decoded transcript PLUS the untouched raw JSON below it, so
    ///     nothing is lost if the renderer doesn't recognise a field.
    ///
    /// Claude's extended-thinking content comes along for free when thinking is enabled — the
    /// adapter's own parsing discards thinking blocks, but that happens after this handler has
    /// already seen the untouched raw body, and AiTraceRenderer surfaces "### ASSISTANT (thinking)".
    ///
    /// Deliberately NEVER logs request/response headers — Claude's x-api-key and the OpenAI-
    /// compatible clients' Authorization/api-key headers carry real secrets, and headers add
    /// nothing a developer needs for reading prompt/response content.
    /// </summary>
    public class AiTracingHandler : DelegatingHandler
    {
        private const string Divider = "================================================================================";

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
            var startedAt = DateTimeOffset.Now;
            var traceFile = Path.Combine(
                ResolveTraceDirectory(),
                $"{callNumber:D4}_{startedAt:yyyyMMdd-HHmmss-fff}.md");

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

            var requestHeader = $"AI call #{callNumber}  ·  {startedAt:yyyy-MM-dd HH:mm:ss.fff}{Environment.NewLine}"
                + $"{request.Method} {request.RequestUri}";
            var renderedRequest = AiTraceRenderer.RenderRequest(requestBody);

            _logger.LogInformation(
                "{Divider}{NewLine}{Header}{NewLine}{NewLine}{Request}",
                Divider, Environment.NewLine, requestHeader, Environment.NewLine, Environment.NewLine, renderedRequest);

            WriteTraceFile(
                traceFile,
                $"{Divider}{Environment.NewLine}{requestHeader}{Environment.NewLine}{Divider}{Environment.NewLine}{Environment.NewLine}"
                + $"{renderedRequest}{Environment.NewLine}"
                + Section("RAW REQUEST JSON", AiTraceRenderer.PrettyJson(requestBody)));

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
                WriteTraceFile(traceFile, Section(
                    $"THREW after {stopwatch.ElapsedMilliseconds}ms",
                    ex.ToString()));
                throw;
            }

            stopwatch.Stop();

            string? responseBody = null;
            if (response.Content is not null)
            {
                await response.Content.LoadIntoBufferAsync();
                responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            }

            var responseHeader = $"AI call #{callNumber} response  ·  {(int)response.StatusCode} {response.StatusCode}  ·  {stopwatch.ElapsedMilliseconds}ms";
            var renderedResponse = AiTraceRenderer.RenderResponse(responseBody);

            _logger.LogInformation(
                "{Divider}{NewLine}{Header}{NewLine}{NewLine}{Response}",
                Divider, Environment.NewLine, responseHeader, Environment.NewLine, Environment.NewLine, renderedResponse);

            WriteTraceFile(
                traceFile,
                $"{Environment.NewLine}{Divider}{Environment.NewLine}{responseHeader}{Environment.NewLine}{Divider}{Environment.NewLine}{Environment.NewLine}"
                + $"{renderedResponse}{Environment.NewLine}"
                + Section("RAW RESPONSE JSON", AiTraceRenderer.PrettyJson(responseBody)));

            return response;
        }

        private static string Section(string title, string body)
            => $"----- {title} -----{Environment.NewLine}{body}{Environment.NewLine}";

        private string ResolveTraceDirectory()
        {
            var configured = Environment.GetEnvironmentVariable("AI_TRACE_DIR");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            var root = string.IsNullOrWhiteSpace(_environment.ContentRootPath)
                ? Directory.GetCurrentDirectory()
                : _environment.ContentRootPath;
            return Path.Combine(root, "logs", "ai-trace");
        }

        private void WriteTraceFile(string path, string content)
        {
            // Tracing must never be able to break a real AI call — swallow every IO failure.
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.AppendAllText(path, content, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "AiTracingHandler could not write transcript file {Path}", path);
            }
        }
    }
}
