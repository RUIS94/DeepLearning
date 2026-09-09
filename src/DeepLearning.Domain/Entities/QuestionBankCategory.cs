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

        /// <summary>
        /// Which exam type this category belongs to. Nullable: NULL is a global category that
        /// shows up under every exam type's config (rows created before this column existed stay
        /// NULL and keep that behaviour). Filtered by IQuestionBankCategoryRepository.ListAsync
        /// as "= examTypeId OR IS NULL".
        /// </summary>
        public Guid? ExamTypeId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public QuestionBankCategory? Parent { get; set; }
        public ExamType? ExamType { get; set; }
    }
}
