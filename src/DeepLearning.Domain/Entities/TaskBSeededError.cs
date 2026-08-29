using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    public class TaskBSeededError : Entity
    {
        public Guid QuestionId { get; set; }
        public int PositionStart { get; set; }
        public int PositionEnd { get; set; }
        public Guid ErrorTaxonomyId { get; set; }
        public string CorrectReferenceText { get; set; } = string.Empty;
        public string? Note { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public Question? Question { get; set; }
        public ErrorTaxonomy? ErrorTaxonomy { get; set; }
    }
}
