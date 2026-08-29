using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Entities
{
    public class QuestionBankCategory : Entity
    {
        public CategoryType CategoryType { get; set; }
        public string Name { get; set; } = string.Empty;
        public Guid? ParentId { get; set; }
        public string? Description { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public QuestionBankCategory? Parent { get; set; }
    }
}
