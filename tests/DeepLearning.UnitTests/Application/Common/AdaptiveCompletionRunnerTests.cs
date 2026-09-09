using DeepLearning.Application.Common;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Ai;

namespace DeepLearning.UnitTests.Application.Common
{
    /// <summary>
    /// <see cref="AdaptiveCompletionRunner"/> is the shared "call the LLM, detect truncation,
    /// retry with a bigger budget, and never hang forever" loop. These tests exercise it with a
    /// stub <see cref="ILlmClient"/> (no HTTP, no DB) and a tiny hard-timeout override so the
    /// 240s production backstop can be proven to actually fire in milliseconds.
    ///
    /// The backstop matters because of the 2026-09-05 incident: a grading call sat in
    /// ai_call_logs at status='calling' for 10+ minutes because HttpClient.SendAsync neither
    /// returned nor threw and Polly's own timeout evidently never fired. Root cause unconfirmed;
    /// this class pins the guarantee that <see cref="IAiCallRetryExecutor"/>'s outer loop is
    /// always eventually handed an exception regardless of what the transport does.
    /// </summary>
    public class AdaptiveCompletionRunnerTests
    {
        private static AiCallLog NewLog(int maxRetries = 3) => new()
        {
            Id = Guid.NewGuid(),
            RequestType = AiOperationType.grading,
            Status = CallStatus.calling,
            AttemptCount = 1,
            MaxRetries = maxRetries,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        private static AiCallRetryExecutor FastExecutor() => new(TimeSpan.FromMilliseconds(1));

        private sealed class StubLlmClient : ILlmClient
        {
            private readonly Queue<Func<CancellationToken, Task<LlmCompletionResult>>> _responses;

            public StubLlmClient(params Func<CancellationToken, Task<LlmCompletionResult>>[] responses)
                => _responses = new Queue<Func<CancellationToken, Task<LlmCompletionResult>>>(responses);

            public List<LlmCompletionRequest> Requests { get; } = [];

            public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
            {
                Requests.Add(request);
                if (_responses.Count == 0)
                {
                    throw new InvalidOperationException("StubLlmClient ran out of stubbed responses.");
                }

                return _responses.Dequeue()(cancellationToken);
            }
        }

        private static Func<CancellationToken, Task<LlmCompletionResult>> Ok(string text, bool truncated = false)
            => _ => Task.FromResult(new LlmCompletionResult(text, InputTokens: 0, OutputTokens: 0, Model: "stub", LatencyMs: 3, Truncated: truncated));

        /// <summary>A call that never completes on its own — only the hard-timeout token ends it.</summary>
        private static Func<CancellationToken, Task<LlmCompletionResult>> Hangs()
            => async ct =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return default!;
            };

        [Fact]
        public async Task Returns_the_parsed_value_on_a_clean_first_attempt()
        {
            var log = NewLog();

            var result = await AdaptiveCompletionRunner.RunAsync(
                FastExecutor(),
                new StubLlmClient(Ok("  hello  ")),
                log,
                prompt: "p",
                initialBudget: 1024,
                maxBudget: 8192,
                parse: s => s.Trim());

            Assert.Equal("hello", result);
            Assert.Equal(1, log.AttemptCount);
        }

        [Fact]
        public async Task Doubles_the_token_budget_and_retries_after_a_provider_reported_truncation()
        {
            var stub = new StubLlmClient(Ok("cut off mid-js", truncated: true), Ok("complete"));
            var log = NewLog(maxRetries: 3);

            var result = await AdaptiveCompletionRunner.RunAsync(
                FastExecutor(),
                stub,
                log,
                prompt: "p",
                initialBudget: 1024,
                maxBudget: 8192,
                parse: s => s);

            Assert.Equal("complete", result);
            Assert.Equal(2, stub.Requests.Count);
            Assert.Equal(1024, stub.Requests[0].MaxTokens);
            Assert.Equal(2048, stub.Requests[1].MaxTokens);
            Assert.Equal(2, log.AttemptCount);
        }

        [Fact]
        public async Task Caps_the_doubled_budget_at_max_budget()
        {
            var stub = new StubLlmClient(
                Ok("t", truncated: true),
                Ok("t", truncated: true),
                Ok("done"));
            var log = NewLog(maxRetries: 5);

            await AdaptiveCompletionRunner.RunAsync(
                FastExecutor(),
                stub,
                log,
                prompt: "p",
                initialBudget: 4000,
                maxBudget: 6000,
                parse: s => s);

            Assert.Equal(4000, stub.Requests[0].MaxTokens);
            Assert.Equal(6000, stub.Requests[1].MaxTokens); // 8000 capped to 6000
            Assert.Equal(6000, stub.Requests[2].MaxTokens);
        }

        [Fact]
        public async Task The_hard_backstop_turns_a_never_returning_call_into_a_TimeoutException()
        {
            var log = NewLog(maxRetries: 1); // no retry — fail on the first attempt

            var run = AdaptiveCompletionRunner.RunAsync(
                FastExecutor(),
                new StubLlmClient(Hangs()),
                log,
                prompt: "p",
                initialBudget: 1024,
                maxBudget: 8192,
                parse: s => s,
                hardAttemptTimeout: TimeSpan.FromMilliseconds(80));

            await Assert.ThrowsAsync<TimeoutException>(() => run);
        }

        [Fact]
        public async Task A_hung_call_is_retried_up_to_max_retries_then_the_TimeoutException_escapes()
        {
            var log = NewLog(maxRetries: 3); // AttemptCount starts at 1

            var run = AdaptiveCompletionRunner.RunAsync(
                FastExecutor(),
                new StubLlmClient(Hangs(), Hangs(), Hangs()),
                log,
                prompt: "p",
                initialBudget: 1024,
                maxBudget: 8192,
                parse: s => s,
                hardAttemptTimeout: TimeSpan.FromMilliseconds(50));

            await Assert.ThrowsAsync<TimeoutException>(() => run);
            Assert.Equal(3, log.AttemptCount); // retried up to the ceiling, never unbounded
        }

        [Fact]
        public async Task A_caller_cancellation_is_not_masked_as_the_backstop_TimeoutException()
        {
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            var run = AdaptiveCompletionRunner.RunAsync(
                FastExecutor(),
                new StubLlmClient(Hangs()),
                NewLog(maxRetries: 1),
                prompt: "p",
                initialBudget: 1024,
                maxBudget: 8192,
                parse: s => s,
                hardAttemptTimeout: TimeSpan.FromMilliseconds(80),
                cancellationToken: cts.Token);

            var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
            Assert.IsNotType<TimeoutException>(ex);
        }
    }
}
