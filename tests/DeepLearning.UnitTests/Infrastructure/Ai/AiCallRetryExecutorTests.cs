using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Ai;

namespace DeepLearning.UnitTests.Infrastructure.Ai
{
    /// <summary>
    /// Design doc §4.2's AI-call retry sub-state-machine, previously dead code (AttemptCount/
    /// MaxRetries were set once at AiCallLog creation and never touched again — every structured-
    /// output validation failure went straight to final_failure on the very first attempt). These
    /// tests exercise AiCallRetryExecutor directly (no DB, no HTTP) with a near-zero delay so they
    /// run fast while still proving the real retry/backoff/give-up logic, not just that it compiles.
    /// </summary>
    public class AiCallRetryExecutorTests
    {
        private static AiCallLog NewLog(int maxRetries = 3) => new()
        {
            Id = Guid.NewGuid(),
            RequestType = AiOperationType.question_gen,
            Status = CallStatus.calling,
            AttemptCount = 1,
            MaxRetries = maxRetries,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        [Fact]
        public async Task Succeeds_on_the_first_attempt_without_incrementing_attempt_count()
        {
            var executor = new AiCallRetryExecutor(TimeSpan.FromMilliseconds(1));
            var log = NewLog();

            var result = await executor.ExecuteAsync(log, () => Task.FromResult(42));

            Assert.Equal(42, result);
            Assert.Equal(1, log.AttemptCount);
        }

        [Fact]
        public async Task Retries_after_a_failure_and_succeeds_on_a_later_attempt_incrementing_attempt_count_for_each_retry()
        {
            var executor = new AiCallRetryExecutor(TimeSpan.FromMilliseconds(1));
            var log = NewLog(maxRetries: 3);
            var callCount = 0;

            var result = await executor.ExecuteAsync(log, () =>
            {
                callCount++;
                if (callCount < 3)
                {
                    throw new InvalidOperationException("simulated validation failure");
                }
                return Task.FromResult("ok");
            });

            Assert.Equal("ok", result);
            Assert.Equal(3, callCount);
            Assert.Equal(3, log.AttemptCount);
        }

        [Fact]
        public async Task Gives_up_after_max_retries_attempts_and_rethrows_the_last_exception()
        {
            var executor = new AiCallRetryExecutor(TimeSpan.FromMilliseconds(1));
            var log = NewLog(maxRetries: 3);
            var callCount = 0;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync<string>(log, () =>
            {
                callCount++;
                throw new InvalidOperationException($"attempt {callCount} failed");
            }));

            // Exactly 3 attempts (not 1, and not unbounded) — the whole point of this fix: a
            // single validation failure used to go straight to final_failure with no retry at all.
            Assert.Equal(3, callCount);
            Assert.Equal(3, log.AttemptCount);
            Assert.Equal("attempt 3 failed", ex.Message);
        }

        [Fact]
        public async Task A_max_retries_of_one_never_retries_at_all()
        {
            var executor = new AiCallRetryExecutor(TimeSpan.FromMilliseconds(1));
            var log = NewLog(maxRetries: 1);
            var callCount = 0;

            await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync<string>(log, () =>
            {
                callCount++;
                throw new InvalidOperationException("fails");
            }));

            Assert.Equal(1, callCount);
            Assert.Equal(1, log.AttemptCount);
        }

        [Fact]
        public async Task Respects_cancellation_during_the_backoff_delay()
        {
            var executor = new AiCallRetryExecutor(TimeSpan.FromSeconds(30));
            var log = NewLog(maxRetries: 3);
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(50));

            await Assert.ThrowsAsync<TaskCanceledException>(() => executor.ExecuteAsync<string>(
                log,
                () => throw new InvalidOperationException("fails"),
                cts.Token));
        }
    }
}
