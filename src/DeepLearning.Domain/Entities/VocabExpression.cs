using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    public class VocabExpression : Entity
    {
        public Guid? QuestionId { get; set; }
        public string EnglishExpr { get; set; } = string.Empty;
        public string? ChineseEquiv { get; set; }
        public string? ContextNote { get; set; }
        public string? Category { get; set; }
        public string? Domain { get; set; }
        public string? Scenario { get; set; }
        public string? FrequencyTag { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public Question? Question { get; set; }
    }
}
