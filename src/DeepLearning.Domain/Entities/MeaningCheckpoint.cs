using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Entities
{
    public class MeaningCheckpoint : Entity
    {
        public Guid QuestionId { get; set; }
        public string CheckpointText { get; set; } = string.Empty;
        public string? CheckpointType { get; set; }
        public CheckpointImportance Importance { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public Question? Question { get; set; }
    }
}
