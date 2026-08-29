using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Entities
{
    public class UserVocabReview : Entity
    {
        public Guid UserId { get; set; }
        public Guid VocabId { get; set; }
        public int TimesEncountered { get; set; } = 1;
        public MasteryLevel MasteryLevel { get; set; } = MasteryLevel.New;
        public DateTimeOffset? LastReviewedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public User? User { get; set; }
        public VocabExpression? Vocab { get; set; }
    }
}
