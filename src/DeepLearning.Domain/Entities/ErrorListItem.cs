using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Entities
{
    public class ErrorListItem : Entity
    {
        public Guid SubmissionId { get; set; }
        public string? PositionRef { get; set; }
        public string? SourceTextSnippet { get; set; }
        public string? UserTextSnippet { get; set; }
        public Guid ErrorTaxonomyId { get; set; }
        public Guid DimensionId { get; set; }

        /// <summary>NAATI's Major/Minor for this one error. Drives the severity badge and the 累积密度 roll-up.</summary>
        public ErrorSeverity Severity { get; set; } = ErrorSeverity.minor;

        /// <summary>
        /// Short summarising label the grader writes per error — a terse characterisation such as
        /// "概念方向偏移" / "术语方向性错误+全文不一致" / "修饰语堆叠". Shown next to the severity badge
        /// (UI composes "严重，{summary}"). ≤ 60 chars.
        /// </summary>
        public string? Summary { get; set; }

        /// <summary>
        /// Legacy boolean kept for back-compat with rows written before <see cref="Severity"/> existed.
        /// No longer asked of the AI — set deterministically to (Severity is major or critical).
        /// </summary>
        public bool ImpactsCore { get; set; }
        public string? Explanation { get; set; }
        public string? Suggestion { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public Submission? Submission { get; set; }
        public ErrorTaxonomy? ErrorTaxonomy { get; set; }
        public AssessmentDimension? Dimension { get; set; }
    }
}
