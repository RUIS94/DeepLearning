using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    public class QuestionCategoryMap : Entity
    {
        public Guid QuestionId { get; set; }
        public Guid CategoryId { get; set; }

        public Question? Question { get; set; }
        public QuestionBankCategory? Category { get; set; }
    }
}
