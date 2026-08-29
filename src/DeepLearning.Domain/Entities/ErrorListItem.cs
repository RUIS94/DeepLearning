using DeepLearning.Domain.Common;

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
        public bool ImpactsCore { get; set; }
        public string? Explanation { get; set; }
        public string? Suggestion { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public Submission? Submission { get; set; }
        public ErrorTaxonomy? ErrorTaxonomy { get; set; }
        public AssessmentDimension? Dimension { get; set; }
    }
}
