using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Entities
{
    public class ExamType : AggregateRoot
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public SubjectCategory SubjectCategory { get; set; }
        public string? SourceLanguage { get; set; }
        public string? TargetLanguage { get; set; }
        public string? GradeLevel { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; }
    }
}
