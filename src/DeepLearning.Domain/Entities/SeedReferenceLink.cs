using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    public class SeedReferenceLink : Entity
    {
        public Guid GeneratedQuestionId { get; set; }
        public Guid SeedQuestionId { get; set; }
        public string? SimilarityReason { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public Question? GeneratedQuestion { get; set; }
        public Question? SeedQuestion { get; set; }
    }
}
