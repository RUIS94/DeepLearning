using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Entities
{
    public class FollowUpQuestion : Entity
    {
        public Guid SubmissionId { get; set; }
        public Guid UserId { get; set; }
        public string? ContextRef { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public string? AiResponse { get; set; }
        public FollowUpVerdict Verdict { get; set; } = FollowUpVerdict.pending;
        public DateTimeOffset CreatedAt { get; set; }

        public Submission? Submission { get; set; }
        public User? User { get; set; }
    }
}
