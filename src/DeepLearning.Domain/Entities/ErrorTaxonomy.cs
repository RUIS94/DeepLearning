using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    public class ErrorTaxonomy : Entity
    {
        public Guid ExamTypeId { get; set; }
        public string CategoryKey { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ExampleCases { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public ExamType? ExamType { get; set; }
    }
}
