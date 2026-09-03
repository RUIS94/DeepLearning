using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Entities
{
    /// <summary>
    /// Curated, per-exam-type list of weak-point kinds (NAATI CT §12's hand-authored categories,
    /// seeded by seed_weak_point_catalog_naati_ct.sql). Decouples weak-point tracking from
    /// UpdateWeakPointsOnGraded's coarse "{DimensionName} - {ErrorCategoryName}" bucketing: an
    /// ErrorListItem is matched to a catalog row via <see cref="DefaultDimensionKey"/> (+ optionally
    /// <see cref="DefaultErrorCategory"/>) or the weak_point_classification AI call, falling back
    /// to the free-text <see cref="WeakPoint.Category"/> bucket only when nothing matches.
    ///
    /// No longer strictly insert-only: rows can also be born <see cref="WeakPointCatalogStatus.proposed"/>
    /// at runtime (AI proposal when a legacy bucket recurs) or via a manual admin entry, then be
    /// approved / renamed / merged. A merge <see cref="WeakPointCatalogStatus.deprecated"/>s the
    /// losing row rather than deleting it.
    /// </summary>
    public class WeakPointCatalog : AggregateRoot
    {
        public Guid ExamTypeId { get; set; }

        /// <summary>Stable identifier, e.g. <c>omission_hedging</c>. Copied to <see cref="WeakPoint.Category"/> when a weak point maps here.</summary>
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        /// <summary>The assessment dimension key errors of this kind usually fall under (nullable = matches any dimension).</summary>
        public string? DefaultDimensionKey { get; set; }

        /// <summary>The error taxonomy key errors of this kind usually carry (nullable = matches any category under the dimension).</summary>
        public string? DefaultErrorCategory { get; set; }

        /// <summary>Lifecycle — see <see cref="WeakPointCatalogStatus"/>. Rule/AI matching considers proposed + active, never deprecated.</summary>
        public WeakPointCatalogStatus Status { get; set; } = WeakPointCatalogStatus.active;

        /// <summary>How the row was created: <c>seed</c> (seed file) | <c>auto</c> (system-minted when a legacy bucket recurred) | <c>manual</c> (admin-entered).</summary>
        public string Origin { get; set; } = "seed";

        public DateTimeOffset CreatedAt { get; set; }

        public ExamType? ExamType { get; set; }
    }
}
