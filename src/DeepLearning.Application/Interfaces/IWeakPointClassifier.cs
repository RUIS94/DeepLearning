using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Interfaces
{
    /// <summary>One graded error handed to the classifier, flattened from ErrorListItem + its navigations.</summary>
    public record WeakPointClassifierError(
        Guid ErrorListId,
        string DimensionKey,
        string ErrorCategoryKey,
        string? Snippet,
        string? Explanation,
        ErrorSeverity Severity);

    /// <summary>
    /// One of the learner's currently-active weak points, passed in so the classifier can merge
    /// its <see cref="PatternSummary"/> with this submission's new evidence instead of writing a
    /// summary from scratch. Catalog-mapped rows only (legacy buckets get a deterministic string,
    /// no AI).
    /// </summary>
    public record ActiveWeakPointSummary(string CatalogCode, string? PatternSummary);

    /// <summary>
    /// A leaf the classifier judged doesn't fit any existing <see cref="WeakPointCatalog"/> code —
    /// created as a <see cref="WeakPointCatalogStatus.proposed"/> row under the named top-level
    /// category, pending admin review. The error(s) that triggered it stay uncatalogued
    /// (<see cref="WeakPointClassificationResult.ErrorToCatalogId"/> has no entry for them) this
    /// round — a not-yet-approved proposal is never used to place an error.
    /// </summary>
    public record ProposedCatalogLeaf(string CategoryCode, string Code, string Name, string Description);

    /// <summary>
    /// Result of one classification pass:
    /// <see cref="ErrorToCatalogId"/> — errorListId -&gt; catalogId for errors the AI could place;
    /// <see cref="CatalogCodeToPatternSummary"/> — catalogCode -&gt; an updated per-learner pattern
    /// summary, for the kinds this submission touched (merged from the prior summary + new evidence);
    /// <see cref="ProposedLeaves"/> — new leaves the AI judged none of the existing ~38 fit.
    /// </summary>
    public record WeakPointClassificationResult(
        IReadOnlyDictionary<Guid, Guid> ErrorToCatalogId,
        IReadOnlyDictionary<string, string> CatalogCodeToPatternSummary,
        IReadOnlyList<ProposedCatalogLeaf> ProposedLeaves)
    {
        public static readonly WeakPointClassificationResult Empty = new(
            new Dictionary<Guid, Guid>(),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            []);
    }

    /// <summary>
    /// Maps a graded submission's errors to <see cref="WeakPointCatalog"/> ids via one AI call
    /// (<c>AiOperationType.weak_point_classification</c>) — for the cases the deterministic
    /// (dimension, error category) rule in UpdateWeakPointsOnGraded can't tell apart (several
    /// distinct weak-point kinds all landing on the same dimension/category pair, e.g. numeric
    /// traps vs. logic-relation distortions vs. look-alike-word confusion, all
    /// meaning_transfer/distortion). The same call also returns a refreshed per-learner
    /// <c>pattern_summary</c> for each kind it touched.
    ///
    /// Contract: NEVER throws. Returns <see cref="WeakPointClassificationResult.Empty"/> on
    /// "no template configured" / "AI call failed" and omits anything it could not confidently
    /// place; everything omitted falls back to the rule, so weak-point tracking is unchanged
    /// from before this existed when the feature is off or unavailable.
    /// </summary>
    public interface IWeakPointClassifier
    {
        Task<WeakPointClassificationResult> ClassifyAsync(
            Guid examTypeId,
            IReadOnlyList<WeakPointClassifierError> errors,
            IReadOnlyList<WeakPointCatalog> catalog,
            IReadOnlyList<ActiveWeakPointSummary> activeWeakPoints,
            CancellationToken cancellationToken = default);
    }
}
