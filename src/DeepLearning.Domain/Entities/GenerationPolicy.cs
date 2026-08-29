using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    public class GenerationPolicy : Entity
    {
        public Guid ExamTypeId { get; set; }
        public string PolicyKey { get; set; } = string.Empty;
        public string PolicyValue { get; set; } = string.Empty;
        public DateTimeOffset UpdatedAt { get; set; }

        public ExamType? ExamType { get; set; }
    }
}
