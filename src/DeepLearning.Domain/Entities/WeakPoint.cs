using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Entities
{
    public class WeakPoint : AggregateRoot
    {
        public Guid UserId { get; set; }

        /// <summary>
        /// Stable grouping key. When <see cref="CatalogId"/> is set this holds the matched
        /// <see cref="WeakPointCatalog.Code"/>; otherwise it is the legacy free-text
        /// "{DimensionName} - {ErrorCategoryName}" bucket UpdateWeakPointsOnGraded falls back to
        /// when no catalog row matches.
        /// </summary>
        public string Category { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTimeOffset FirstDetectedAt { get; set; }
        public DateTimeOffset LastSeenAt { get; set; }
        public int RecurrenceCount { get; set; }
        public WeakPointStatus Status { get; set; } = WeakPointStatus.active;
        public Priority Priority { get; set; } = Priority.medium;

        /// <summary>Which exam type this weak point was detected under. Nullable for rows written before this column existed.</summary>
        public Guid? ExamTypeId { get; set; }

        /// <summary>The curated <see cref="WeakPointCatalog"/> row this weak point maps to, or null for a legacy free-text bucket.</summary>
        public Guid? CatalogId { get; set; }

        /// <summary>How this weak point was produced: <c>rule</c> (deterministic bucketing), <c>ai</c>, <c>seed</c> or <c>manual</c>.</summary>
        public string DetectionSource { get; set; } = "rule";

        /// <summary>Set when a resolve sweep marks the weak point cleared; a later occurrence flips <see cref="Status"/> back to active and counts as a recurrence.</summary>
        public DateTimeOffset? ResolvedAt { get; set; }

        /// <summary>Short human-readable note on the most recent detection (which error / which submission), for review UIs.</summary>
        public string? EvidenceNote { get; set; }

        public User? User { get; set; }
        public WeakPointCatalog? Catalog { get; set; }
    }
}
