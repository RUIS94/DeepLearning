using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Entities
{
    public class KnowledgePoint : Entity
    {
        public Guid ExamTypeId { get; set; }
        public Guid? QuestionId { get; set; }
        public KnowledgeItemType ItemType { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Payload { get; set; }
        public string? Domain { get; set; }
        public string? Scenario { get; set; }
        public string? FrequencyTag { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public ExamType? ExamType { get; set; }
        public Question? Question { get; set; }
    }
}
