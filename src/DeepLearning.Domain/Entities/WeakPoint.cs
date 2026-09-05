using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Entities
{
    public class WeakPoint : AggregateRoot
    {
        public Guid UserId { get; set; }

        /// <summary>
        /// Legacy free-text grouping key "{DimensionName} - {ErrorCategoryName}", used only while
        /// <see cref="CatalogId"/> is null. Once a weak point is mapped to a catalog kind this is
        /// null and <see cref="CatalogId"/> / <see cref="Catalog"/> is the identity. Governed by
        /// the partial unique index ux_weak_points_user_category (WHERE catalog_id IS NULL).
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// AI-distilled, per-learner rolling description of how THIS user manifests this weak
        /// point — merged from prior summary + each submission's new evidence by the
        /// weak_point_classification call. This is the text injected into the grading prompt
        /// (falling back to <see cref="WeakPointCatalog.Description"/> only until the first
        /// summary is computed). Legacy (catalog-less) buckets get a deterministic string here,
        /// not an AI call.
        /// </summary>
        public string? PatternSummary { get; set; }

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

        /// <summary>
        /// Lifetime count of distinct submissions that hit this weak point — never reset, incremented
        /// on every hit regardless of <see cref="Status"/>. Only read while <see cref="Status"/> is
        /// <see cref="WeakPointStatus.tracking"/>, to decide the 3-submission confirmation threshold;
        /// once past that it is kept purely as history (策划书 §2).
        /// </summary>
        public int OccurrenceSubmissionCount { get; set; }

        /// <summary>
        /// AI-generated, executable rule for spotting this weak point's trap in a fresh source text
        /// and judging whether the translation handled it — the weak_point_recheck call's input, not
        /// shown to the grading prompt (that only sees <see cref="PatternSummary"/>). Generated once
        /// when this weak point first crosses the tracking threshold, and regenerated with fresh
        /// evidence whenever it resurfaces after being resolved.
        /// </summary>
        public string? DetectionCriteria { get; set; }

        /// <summary>
        /// Consecutive weak_point_recheck calls that returned "not_present" (the source text simply
        /// didn't contain this trap, so the check was inconclusive either way). Reset to 0 on any hit
        /// in a new submission's classification or a "still_weak" recheck result; reaching 5 resolves
        /// the weak point for lack of any recent opportunity to confirm it either way (策划书 §2/§3).
        /// </summary>
        public int NoEvidenceStreak { get; set; }

        public User? User { get; set; }
        public WeakPointCatalog? Catalog { get; set; }
    }
}
