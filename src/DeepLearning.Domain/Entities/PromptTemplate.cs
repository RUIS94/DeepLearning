using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Entities
{
    public class PromptTemplate : Entity
    {
        public Guid? ExamTypeId { get; set; }
        public SubjectCategory? SubjectCategory { get; set; }
        public AiOperationType TemplateType { get; set; }
        public TemplateLayer Layer { get; set; }
        public string TemplateContent { get; set; } = string.Empty;
        public int Version { get; set; } = 1;
        public bool IsActive { get; set; } = true;
        public DateTimeOffset CreatedAt { get; set; }

        public ExamType? ExamType { get; set; }
    }
}
