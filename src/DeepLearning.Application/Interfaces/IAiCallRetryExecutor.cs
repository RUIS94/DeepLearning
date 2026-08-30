using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    /// <summary>
    /// Design doc §4.2's AI-call retry sub-state-machine (Pending→Calling→{Success,Failed},
    /// Failed→Pending on retry / →FinalFailure past max_retries) — this is the layer Polly's
    /// resilience pipeline (LlmResiliencePipeline) doesn't cover: Polly retries HTTP-transport
    /// failures (timeouts/429/5xx) INSIDE one ILlmClient.CompleteAsync call; this executor retries
    /// the call *itself* (re-prompting) when the AI answered with a 200 whose content fails
    /// structured-output validation (bad JSON, an errorCategory not in the taxonomy, etc.) — a
    /// class of failure Polly has no way to see, since it never inspects response bodies.
    /// </summary>
    public interface IAiCallRetryExecutor
    {
        /// <summary>
        /// Runs <paramref name="attempt"/>; on failure, increments <paramref name="log"/>'s
        /// AttemptCount and retries (exponential backoff + jitter, design doc §7's "2s/4s/8s+随机
        /// 抖动") as long as AttemptCount is still below MaxRetries, otherwise rethrows the last
        /// exception unchanged so the caller's own catch/FailAsync logic decides the final state.
        /// </summary>
        Task<T> ExecuteAsync<T>(AiCallLog log, Func<Task<T>> attempt, CancellationToken cancellationToken = default);
    }
}
