using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    /// <summary>
    /// Curated, per-exam-type list of weak-point kinds (NAATI CT §12's ten hand-authored
    /// categories, seeded by seed_weak_point_catalog_naati_ct.sql). Decouples weak-point
    /// tracking from UpdateWeakPointsOnGraded's coarse "{DimensionName} - {ErrorCategoryName}"
    /// bucketing: an ErrorListItem is matched to a catalog row via
    /// <see cref="DefaultDimensionKey"/> (+ optionally <see cref="DefaultErrorCategory"/>),
    /// falling back to the free-text <see cref="WeakPoint.Category"/> only when nothing matches.
    /// Insert-only reference data — never rewritten at runtime.
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

        public bool IsActive { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; }

        public ExamType? ExamType { get; set; }
    }
}
