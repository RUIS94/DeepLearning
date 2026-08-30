using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;

namespace DeepLearning.Infrastructure.Ai
{
    /// <inheritdoc cref="IAiCallRetryExecutor"/>
    public class AiCallRetryExecutor : IAiCallRetryExecutor
    {
        private readonly TimeSpan _baseDelay;

        /// <param name="baseDelay">
        /// Delay before the first retry; doubles each subsequent retry (2s/4s/8s... by default,
        /// matching design doc §7). Overridable so tests don't have to sleep through real backoffs
        /// — production wiring (DependencyInjection.cs) uses the 2-second default.
        /// </param>
        public AiCallRetryExecutor(TimeSpan? baseDelay = null)
        {
            _baseDelay = baseDelay ?? TimeSpan.FromSeconds(2);
        }

        public async Task<T> ExecuteAsync<T>(AiCallLog log, Func<Task<T>> attempt, CancellationToken cancellationToken = default)
        {
            while (true)
            {
                try
                {
                    return await attempt();
                }
                catch when (log.AttemptCount < log.MaxRetries)
                {
                    // AttemptCount 1 -> 2s, 2 -> 4s, 3 -> 8s, ... plus jitter so a burst of
                    // simultaneously-failing calls doesn't retry in lockstep.
                    var backoff = TimeSpan.FromTicks(_baseDelay.Ticks * (1L << (log.AttemptCount - 1)));
                    var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 500));
                    await Task.Delay(backoff + jitter, cancellationToken);
                    log.AttemptCount++;
                }
            }
        }
    }
}
