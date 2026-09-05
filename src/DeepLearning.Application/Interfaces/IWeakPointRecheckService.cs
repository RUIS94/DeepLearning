namespace DeepLearning.Application.Interfaces
{
    /// <summary>One active weak point not hit by this submission's classification, up for recheck.</summary>
    public record WeakPointRecheckCandidate(Guid WeakPointId, string CatalogCode, string DetectionCriteria);

    /// <summary>Three-way verdict for one code — see 薄弱点分类与生命周期管理_策划书.md §3.</summary>
    public enum WeakPointRecheckOutcome
    {
        /// <summary>The trap is present in the source text and the translation handled it correctly — safe to deactivate.</summary>
        Resolved,
        /// <summary>The trap is present and the translation still mishandled it — real evidence, stays active.</summary>
        StillWeak,
        /// <summary>The source text doesn't contain this trap at all — inconclusive, no evidence either way.</summary>
        NotPresent,
    }

    /// <summary>
    /// Runs <c>AiOperationType.weak_point_recheck</c> — one batched call covering every candidate
    /// for a single submission, checking each candidate's <see cref="WeakPointRecheckCandidate.DetectionCriteria"/>
    /// against this submission's source text and the user's translation. Does NOT write
    /// error_list, does NOT increment RecurrenceCount/OccurrenceSubmissionCount — a recheck is a
    /// status judgment, not a new occurrence. Contract: NEVER throws; a candidate omitted from the
    /// result is treated as unresolved (caller should leave its status/streak untouched).
    /// </summary>
    public interface IWeakPointRecheckService
    {
        Task<IReadOnlyDictionary<Guid, WeakPointRecheckOutcome>> RecheckAsync(
            Guid examTypeId,
            IReadOnlyList<WeakPointRecheckCandidate> candidates,
            string sourceText,
            string translationText,
            CancellationToken cancellationToken = default);
    }
}
