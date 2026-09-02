using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    /// <summary>
    /// One holistic row per graded submission, sitting above the per-dimension
    /// <see cref="GradingResult"/> rows. Design doc §11 (c): the assessor's subjective
    /// "本篇译文通过概率" plus the whole-submission pass/fail (NAATI requires every dimension to
    /// pass) and a rolled-up cumulative-density note. Derived deterministically by
    /// GradeSubmissionCommandHandler from the per-dimension results — not a separate AI field.
    /// One-to-one with the submission (unique index on <see cref="SubmissionId"/>); re-graded
    /// after a failure it is upserted, never duplicated.
    /// </summary>
    public class GradingSummary : Entity
    {
        public Guid SubmissionId { get; set; }

        /// <summary>0..1, the product of the per-dimension estimated pass probabilities (missing ones treated as 1.0).</summary>
        public decimal OverallPassProbability { get; set; }

        /// <summary>True only when every assessed dimension passed its threshold.</summary>
        public bool OverallPassBool { get; set; }

        /// <summary>True when any dimension flagged cumulative density as a downgrade risk.</summary>
        public bool CumulativeDensityFlag { get; set; }

        /// <summary>Rolled-up cumulative-density notes from the dimensions that raised one.</summary>
        public string? CumulativeDensityNote { get; set; }

        /// <summary>Optional free-text overall conclusion (reserved; not populated yet).</summary>
        public string? ConclusionText { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public Submission? Submission { get; set; }
    }
}
