using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    /// <summary>One graded error handed to the classifier, flattened from ErrorListItem + its navigations.</summary>
    public record WeakPointClassifierError(
        Guid ErrorListId,
        string DimensionKey,
        string ErrorCategoryKey,
        string? Snippet,
        string? Explanation,
        bool ImpactsCore);

    /// <summary>
    /// Maps a graded submission's errors to <see cref="WeakPointCatalog"/> ids via one AI call
    /// (<c>AiOperationType.weak_point_classification</c>) — for the cases the deterministic
    /// (dimension, error category) rule in UpdateWeakPointsOnGraded can't tell apart (several
    /// distinct weak-point kinds all landing on the same dimension/category pair, e.g. numeric
    /// traps vs. logic-relation distortions vs. look-alike-word confusion, all
    /// meaning_transfer/distortion).
    ///
    /// Contract: NEVER throws. Returns <c>errorListId -&gt; catalogId</c> only for errors it
    /// could confidently place; everything it omits (and the empty result on "no template
    /// configured" / "AI call failed") falls back to the rule, so weak-point tracking is
    /// unchanged from before this existed when the feature is off or unavailable.
    /// </summary>
    public interface IWeakPointClassifier
    {
        Task<IReadOnlyDictionary<Guid, Guid>> ClassifyAsync(
            Guid examTypeId,
            IReadOnlyList<WeakPointClassifierError> errors,
            IReadOnlyList<WeakPointCatalog> catalog,
            CancellationToken cancellationToken = default);
    }
}
