using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    public class ReferenceTranslation : Entity
    {
        public Guid QuestionId { get; set; }
        public string ReferenceText { get; set; } = string.Empty;
        public string? ComparisonNotes { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public Question? Question { get; set; }
    }
}
