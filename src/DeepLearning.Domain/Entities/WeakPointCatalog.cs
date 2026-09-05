using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Entities
{
    /// <summary>
    /// Curated, globally-shared (not per-exam-type) list of weak-point leaf kinds — the two-level
    /// linguistic taxonomy's leaves, each hanging off a <see cref="WeakPointCategory"/> (薄弱点分类
    /// 与生命周期管理_策划书.md §1). Global because the taxonomy is linguistic, not exam-specific: a
    /// causality (因果) mix-up is the same weak point whether it showed up in a NAATI CT or any
    /// other translation exam. Decouples weak-point tracking from UpdateWeakPointsOnGraded's coarse
    /// "{DimensionName} - {ErrorCategoryName}" bucketing: an ErrorListItem is matched to a catalog
    /// row via <see cref="DefaultDimensionKey"/> (+ optionally <see cref="DefaultErrorCategory"/>)
    /// or the weak_point_classification AI call, falling back to the free-text
    /// <see cref="WeakPoint.Category"/> bucket only when nothing matches.
    ///
    /// No longer strictly insert-only: rows can also be born <see cref="WeakPointCatalogStatus.proposed"/>
    /// at runtime (AI proposal when a legacy bucket recurs, or when the classifier judges none of
    /// the existing leaves fit) or via a manual admin entry, then be approved / renamed / merged. A
    /// merge <see cref="WeakPointCatalogStatus.deprecated"/>s the losing row rather than deleting it.
    /// A proposed row born from an AI suggestion may not yet know its <see cref="CategoryId"/> —
    /// admin review assigns it before approving to <c>active</c>.
    /// </summary>
    public class WeakPointCatalog : AggregateRoot
    {
        /// <summary>The one-level-up bucket in the fixed 8-category taxonomy. Nullable only for a freshly proposed row pending admin triage.</summary>
        public Guid? CategoryId { get; set; }

        /// <summary>Stable identifier, e.g. <c>semantic_causality</c>. Copied to <see cref="WeakPoint.Category"/> when a weak point maps here.</summary>
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        /// <summary>The assessment dimension key errors of this kind usually fall under (nullable = matches any dimension).</summary>
        public string? DefaultDimensionKey { get; set; }

        /// <summary>The error taxonomy key errors of this kind usually carry (nullable = matches any category under the dimension).</summary>
        public string? DefaultErrorCategory { get; set; }

        /// <summary>Lifecycle — see <see cref="WeakPointCatalogStatus"/>. Rule/AI matching considers proposed + active, never deprecated.</summary>
        public WeakPointCatalogStatus Status { get; set; } = WeakPointCatalogStatus.active;

        /// <summary>How the row was created: <c>seed</c> (seed file) | <c>auto</c> (system-minted when a legacy bucket recurred or the classifier proposed a new leaf) | <c>manual</c> (admin-entered).</summary>
        public string Origin { get; set; } = "seed";

        public DateTimeOffset CreatedAt { get; set; }

        public WeakPointCategory? Category { get; set; }
    }
}
