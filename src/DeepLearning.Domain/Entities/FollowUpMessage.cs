using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Entities
{
    /// <summary>
    /// One turn in a FollowUpThread. Verdict is only ever set on an 'ai' message and is that
    /// round's informal opinion for the UI to show — it never drives a state transition or a
    /// StandardOverride by itself; see FollowUpThread's doc comment.
    /// </summary>
    public class FollowUpMessage : Entity
    {
        public Guid ThreadId { get; set; }
        public FollowUpMessageRole Role { get; set; }
        public string Content { get; set; } = string.Empty;
        public FollowUpVerdict? Verdict { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public FollowUpThread? Thread { get; set; }
    }
}
