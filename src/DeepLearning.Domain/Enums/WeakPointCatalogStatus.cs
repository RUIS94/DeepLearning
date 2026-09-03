namespace DeepLearning.Domain.Enums
{
    /// <summary>
    /// Lifecycle of a <see cref="Entities.WeakPointCatalog"/> row.
    /// <c>proposed</c>: minted at runtime (AI proposal on a recurring legacy bucket, or a
    /// pending manual entry) — already usable as a <c>catalog_id</c> target so dedup works from
    /// the second occurrence, but flagged for human review.
    /// <c>active</c>: curated / approved. Seed rows start here.
    /// <c>deprecated</c>: retired, usually after being merged into another kind — kept (not
    /// deleted) so existing <c>weak_points.catalog_id</c> references stay valid and history is
    /// auditable. Excluded from rule/AI matching.
    /// </summary>
    public enum WeakPointCatalogStatus
    {
        proposed,
        active,
        deprecated
    }
}
