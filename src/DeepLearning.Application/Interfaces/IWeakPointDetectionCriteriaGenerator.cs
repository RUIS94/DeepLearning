using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    /// <summary>One piece of historical evidence for a weak point, across all its past submissions (not just the current one).</summary>
    public record WeakPointHistoricalError(string? Snippet, string? Explanation);

    /// <summary>One weak point that just crossed the tracking threshold, or resurfaced after being resolved, and needs a (re)generated screening rule.</summary>
    public record WeakPointDetectionCriteriaRequest(
        Guid WeakPointId,
        string CatalogCode,
        string CatalogName,
        string CatalogDescription,
        IReadOnlyList<WeakPointHistoricalError> HistoricalErrors);

    /// <summary>
    /// Generates <c>AiOperationType.weak_point_detection_criteria</c> — the executable
    /// "how to spot this trap in a fresh source text, and judge whether the translation handled
    /// it" rule stored on <see cref="Entities.WeakPoint.DetectionCriteria"/> and consumed by
    /// <see cref="IWeakPointRecheckService"/>. One batched call for however many weak points crossed
    /// the threshold or resurfaced in this submission's run (usually 0 or 1) — see 薄弱点分类与生命
    /// 周期管理_策划书.md §3. Contract: NEVER throws; a weak point omitted from the result keeps its
    /// current (or null) DetectionCriteria rather than blocking the tracking→active transition.
    /// </summary>
    public interface IWeakPointDetectionCriteriaGenerator
    {
        /// <returns>WeakPointId -&gt; generated detection criteria text, for the requests it could confidently produce one for.</returns>
        Task<IReadOnlyDictionary<Guid, string>> GenerateAsync(
            Guid examTypeId,
            IReadOnlyList<WeakPointDetectionCriteriaRequest> requests,
            CancellationToken cancellationToken = default);
    }
}
