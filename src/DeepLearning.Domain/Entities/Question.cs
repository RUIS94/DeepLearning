using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Entities
{
    public class Question : AggregateRoot
    {
        public TaskType TaskType { get; set; }
        public Difficulty Difficulty { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Brief { get; set; }
        public string SourceText { get; set; } = string.Empty;
        public string? FlawedTranslationText { get; set; }
        public int? WordCount { get; set; }
        public QuestionOrigin Origin { get; set; }
        public SourceType SourceType { get; set; }
        public bool IsSeedReference { get; set; }
        public bool InBank { get; set; }
        public Visibility Visibility { get; set; } = Enums.Visibility.Private;
        public Guid? CreatedBy { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        public User? Creator { get; set; }
    }
}
