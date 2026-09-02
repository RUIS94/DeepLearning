using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Entities
{
    public class Question : AggregateRoot
    {
        public TaskType TaskType { get; set; }
        public Difficulty Difficulty { get; set; }
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Raw Translation Brief as a JSON string (kept for backward compatibility and as the
        /// frontend's display source). The four <c>Brief*</c> columns below carry the same
        /// content in structured form for querying/filtering; readers should prefer them and fall
        /// back to parsing this.
        /// </summary>
        public string? Brief { get; set; }
        public string? BriefDomain { get; set; }
        public string? BriefTextType { get; set; }
        public string? BriefPurpose { get; set; }
        public string? BriefAudience { get; set; }
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
