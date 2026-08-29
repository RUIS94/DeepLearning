using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    public class SentencePattern : Entity
    {
        public Guid? QuestionId { get; set; }
        public string PatternName { get; set; } = string.Empty;
        public string? ExampleSentence { get; set; }
        public string? BreakdownSteps { get; set; }
        public string? Variants { get; set; }
        public string? Domain { get; set; }
        public string? Scenario { get; set; }
        public string? FrequencyTag { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public Question? Question { get; set; }
    }
}
