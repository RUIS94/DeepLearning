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
        public decimal? EstimatedPassProbability { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public Submission? Submission { get; set; }
        public AssessmentDimension? Dimension { get; set; }
    }
}
