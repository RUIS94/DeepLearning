using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Entities
{
    public class Submission : AggregateRoot
    {
        public Guid QuestionId { get; set; }
        public Guid UserId { get; set; }
        public TaskType TaskType { get; set; }
        public string Content { get; set; } = string.Empty;
        public SubmissionStatus Status { get; set; } = SubmissionStatus.draft;
        public DateTimeOffset? SubmittedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        public Question? Question { get; set; }
        public User? User { get; set; }
    }
}
