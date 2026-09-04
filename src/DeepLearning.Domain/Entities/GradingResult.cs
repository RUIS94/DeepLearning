using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    public class GradingResult : Entity
    {
        public Guid SubmissionId { get; set; }
        public Guid DimensionId { get; set; }
        public string RubricVersion { get; set; } = string.Empty;
        public int Band { get; set; }
        public bool PassBool { get; set; }
        public string Rationale { get; set; } = string.Empty;
        public bool CumulativeDensityFlag { get; set; }
        public string? CumulativeDensityNote { get; set; }
        /// <summary>
        /// Heuristic P(this dimension passes). Computed by
        /// GradeSubmissionCommandHandler.EstimateDimensionPassProbability from Band, the
        /// dimension's pass threshold, <see cref="Confidence"/> and
        /// <see cref="CumulativeDensityFlag"/> — never taken from the AI, which stamped one
        /// gut number into every dimension (see rebuild_grading_prompt_three_stage.sql).
        /// </summary>
        public decimal? EstimatedPassProbability { get; set; }

        /// <summary>
        /// How firmly the verdict stage settled on <see cref="Band"/>: high | medium | low.
        /// Null on rows graded before the three-stage prompt existed.
        /// </summary>
        public string? Confidence { get; set; }

        /// <summary>
        /// The runner-up band the verdict stage considered second-best fit — equal to
        /// <see cref="Band"/> when there was no genuine second choice. Null on legacy rows.
        /// </summary>
        public int? AlternativeBand { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public Submission? Submission { get; set; }
        public AssessmentDimension? Dimension { get; set; }
    }
}
