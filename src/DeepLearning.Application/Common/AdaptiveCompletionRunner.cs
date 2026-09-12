using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Common
{
    /// <summary>
    /// Shared "call the LLM, detect truncation, retry with a bigger budget and a corrective
    /// notice" loop — originally grading-only (<c>GradeSubmissionCommandHandler.RunStageAsync</c>),
    /// generalised after the same "output cut off at a too-small fixed MaxTokens -> JSON parse
    /// throws -> blind identical-prompt retry succeeds only by luck" failure mode was confirmed
    /// for weak_point_classification (23 errors spanning 11 catalog codes truncated at 2048
    /// tokens on 2026-09-05) and found to be latent in every other AI call site in the app except
    /// grading (none of them checked <see cref="LlmCompletionResult.Truncated"/> at all).
    ///
    /// Every retry (whether from truncation or a validation/parse failure) drives through
    /// <see cref="IAiCallRetryExecutor"/> exactly as before — this only adds: (1) checking
    /// <c>Truncated</c> before parsing, since a truncated payload's parse error names whatever
    /// field the cut landed in and reads exactly like a bad value in it; (2) doubling the token
    /// budget (capped at <paramref name="maxBudget"/>) on a truncated attempt so the next try has
    /// room; (3) appending a corrective notice on any retry so a temperature-0 call doesn't just
    /// re-produce the same bad answer (see 2026-09-04's incident in the doc comment this replaces).
    /// </summary>
    public static class AdaptiveCompletionRunner
    {
        /// <summary>
        /// Hard backstop above LlmResiliencePipeline's own 60s-per-attempt/180s-total transport
        /// timeout — a safety net for the 2026-09-05 incident where a grading call sat in
        /// ai_call_logs with status='calling' and attempt_count stuck at 1 for 10+ minutes: the
        /// underlying HttpClient.SendAsync neither returned nor threw, so Polly's own
        /// AttemptTimeout/TotalRequestTimeout evidently never fired for it. Root cause unconfirmed
        /// (no way to attach to the hung process after the fact) — this does not depend on Polly's
        /// internals at all, so it is not a fix for whatever that was, only a guarantee that it
        /// cannot hang forever again: <see cref="CancellationTokenSource.CancelAfter(TimeSpan)"/>
        /// fires unconditionally, so <see cref="IAiCallRetryExecutor"/>'s outer retry loop is
        /// always eventually given an exception to act on.
        /// </summary>
        private static readonly TimeSpan HardAttemptTimeout = TimeSpan.FromSeconds(240);

        public static Task<T> RunAsync<T>(
            IAiCallRetryExecutor retryExecutor,
            ILlmClient llmClient,
            AiCallLog log,
            string prompt,
            int initialBudget,
            int maxBudget,
            Func<string, T> parse,
            Action<T>? validate = null,
            decimal? temperature = null,
            Func<string?, bool, string?, string>? buildRejectionNotice = null,
            Action<Exception, bool>? onAttemptFailed = null,
            TimeSpan? hardAttemptTimeout = null,
            CancellationToken cancellationToken = default)
        {
            string? rejectionReason = null;
            var lastAttemptWasTruncated = false;
            // The raw text of the previous attempt, so a re-prompt can show the model what it
            // actually wrote instead of just describing the mistake in the abstract. Without this,
            // "只修正这一处,其余判断保持不变" (fix only this, keep the rest) is an instruction the
            // model has no way to obey — a stateless completion call has no memory of its own prior
            // output, so "keep the rest the same" can only mean re-guessing it from scratch, which
            // is exactly the "full regeneration instead of a minimal fix" behaviour this closes.
            string? lastRawText = null;
            var budget = initialBudget;
            var notice = buildRejectionNotice ?? BuildDefaultRejectionNotice;
            // Overridable only so tests can prove the backstop actually fires without waiting the
            // full 240s; every production call site leaves it null.
            var backstop = hardAttemptTimeout ?? HardAttemptTimeout;

            return retryExecutor.ExecuteAsync(log, async () =>
            {
                using var hardTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                hardTimeoutCts.CancelAfter(backstop);

                LlmCompletionResult completion;
                try
                {
                    completion = await llmClient.CompleteAsync(
                        new LlmCompletionRequest(
                            SystemPrompt: null,
                            UserPrompt: prompt + notice(rejectionReason, lastAttemptWasTruncated, lastRawText),
                            MaxTokens: budget,
                            Temperature: temperature),
                        hardTimeoutCts.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Distinguishes "our own backstop fired" from "the caller's token was
                    // cancelled" (e.g. the request was legitimately aborted upstream) — only the
                    // former is a bug worth a message that says so explicitly in ai_call_logs.
                    throw new TimeoutException(
                        $"LLM call did not complete within the {backstop.TotalSeconds:0}s hard backstop " +
                        "— the transport-level resilience timeout should have failed this well before that and didn't.");
                }

                log.LatencyMs = (log.LatencyMs ?? 0) + completion.LatencyMs;
                lastRawText = completion.Text;

                try
                {
                    if (completion.Truncated)
                    {
                        throw new InvalidOperationException(
                            $"output was cut off at the {budget}-token cap (provider reported truncation), not malformed.");
                    }

                    var parsed = parse(completion.Text);
                    validate?.Invoke(parsed);
                    lastAttemptWasTruncated = false;
                    return parsed;
                }
                catch (Exception ex)
                {
                    rejectionReason = ex.Message;
                    lastAttemptWasTruncated = completion.Truncated;
                    onAttemptFailed?.Invoke(ex, completion.Truncated);

                    // Doubling once or twice covers every payload measured so far and is capped
                    // so a runaway response cannot make each retry more expensive than the last.
                    if (completion.Truncated)
                    {
                        budget = Math.Min(budget * 2, maxBudget);
                    }

                    throw;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Generic corrective notice for call sites without field-specific guidance to give (see
        /// GradeSubmissionCommandHandler.BuildRejectionNotice for a tailored example — pass your
        /// own via <c>buildRejectionNotice</c> when you have specific, commonly-confused fields to
        /// call out).
        ///
        /// <para>Each retry is still a fresh, stateless completion call (no conversation history —
        /// see <see cref="RunAsync{T}"/>'s doc comment), so "keep everything else the same" is not
        /// something the model can honour unless it is actually shown what "everything else" was.
        /// <paramref name="lastRawText"/> is that prior output, quoted back verbatim, so the model
        /// edits the flagged spot in place instead of re-generating the whole answer from memory —
        /// which, for a validation failure like an out-of-bounds character offset, would just
        /// reproduce the same class of mistake against a brand-new text.</para>
        /// </summary>
        private static string BuildDefaultRejectionNotice(string? rejectionReason, bool truncated, string? lastRawText)
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                return string.Empty;
            }

            var header = "\n\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n"
                + "上一次输出已被系统拒绝,请重新输出。\n"
                + "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n"
                + $"拒绝原因:{rejectionReason}\n";

            if (truncated)
            {
                return header
                    + "上一次输出【没有写完】就被截断了,不是格式错误,判断本身也没有问题。\n"
                    + "这一次请把同样的内容【完整】写出来,可以省略不必要的长篇论述,"
                    + "但【不要因此减少任何必需的条目】。\n";
            }

            var previousOutputBlock = string.IsNullOrEmpty(lastRawText)
                ? string.Empty
                : "\n上一次的完整输出如下,供你逐字核对、直接在其基础上修改——不要凭记忆重新编写:\n"
                    + "------\n" + lastRawText + "\n------\n";

            return header
                + previousOutputBlock
                + "请只修正上面这处问题涉及的字段,其余内容原样保留,然后重新输出【修正后的完整】JSON:"
                + "不要使用 markdown 代码块围栏,不要输出任何多余文字。\n";
        }
    }
}
