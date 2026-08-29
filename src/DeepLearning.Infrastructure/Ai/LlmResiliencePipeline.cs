using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace DeepLearning.Infrastructure.Ai
{
    /// <summary>
    /// The retry/circuit-breaker policy shared by every LLM provider adapter (Claude,
    /// OpenAI-compatible providers): up to 3 retries with exponential backoff (2s/4s/8s +
    /// jitter) on 429/5xx/network failures, then a circuit breaker on sustained failure —
    /// matches the design doc's "最多3次重试,指数退避,连续失败触发熔断" non-functional
    /// requirement. One shared policy for all providers, applied to each provider's own
    /// named HttpClient at registration time (see DependencyInjection.cs).
    ///
    /// <see cref="BuildRetryOptions"/> takes an explicit base delay (rather than hardcoding
    /// production's 2s) so a unit test can exercise the exact same retry-count/ShouldHandle
    /// logic with a millisecond-scale delay instead of really sleeping through 2s/4s/8s.
    /// (An earlier version tried to fake this via an injected TimeProvider + Polly's own
    /// delay timer — FakeTimeProvider's auto-advance never actually fired Polly's timer,
    /// which hung the test suite indefinitely. A real, tiny delay is simpler and reliable.)
    /// </summary>
    public static class LlmResiliencePipeline
    {
        private static readonly TimeSpan ProductionBaseDelay = TimeSpan.FromSeconds(2);

        public static void Configure(HttpStandardResilienceOptions options)
        {
            var retry = BuildRetryOptions(ProductionBaseDelay);
            options.Retry.MaxRetryAttempts = retry.MaxRetryAttempts;
            options.Retry.BackoffType = retry.BackoffType;
            options.Retry.Delay = retry.Delay;
            options.Retry.UseJitter = retry.UseJitter;
            options.Retry.ShouldHandle = retry.ShouldHandle;

            // The library defaults (10s per attempt / 30s total) are tuned for typical REST
            // calls, not an LLM completion — a question-generation call with adaptive
            // thinking genuinely took >10s for real against Claude on 2026-08-29. Generous
            // enough to cover 3 retries without the total timeout firing before the retry
            // policy above gets a chance to run its course.
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(180);
            // Must be >= 2x AttemptTimeout or the standard handler's startup validation
            // rejects the config outright (hit for real: "sampling duration ... needs to be
            // at least double of an attempt timeout").
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(150);
        }

        public static Polly.Retry.RetryStrategyOptions<HttpResponseMessage> BuildRetryOptions(TimeSpan baseDelay) => new()
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            Delay = baseDelay,
            UseJitter = true,
            ShouldHandle = args => ValueTask.FromResult(ShouldRetry(args.Outcome)),
        };

        /// <summary>
        /// Builds a standalone pipeline (bypassing HttpClientFactory entirely) with the same
        /// retry logic as production but a caller-supplied base delay — tests pass a
        /// millisecond-scale delay so the exponential backoff still genuinely runs, just fast.
        /// </summary>
        public static ResiliencePipeline<HttpResponseMessage> BuildTestPipeline(TimeSpan baseDelay) =>
            new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddRetry(BuildRetryOptions(baseDelay))
                .Build();

        private static bool ShouldRetry(Outcome<HttpResponseMessage> outcome)
        {
            if (outcome.Exception is not null)
            {
                return true;
            }

            var statusCode = outcome.Result?.StatusCode;
            return statusCode == HttpStatusCode.TooManyRequests || (int?)statusCode >= 500;
        }
    }
}
