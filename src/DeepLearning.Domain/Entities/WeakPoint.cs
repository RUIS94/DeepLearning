using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Entities
{
    public class WeakPoint : AggregateRoot
    {
        public Guid UserId { get; set; }
        public string Category { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTimeOffset FirstDetectedAt { get; set; }
        public DateTimeOffset LastSeenAt { get; set; }
        public int RecurrenceCount { get; set; }
        public WeakPointStatus Status { get; set; } = WeakPointStatus.active;
        public Priority Priority { get; set; } = Priority.medium;

        public User? User { get; set; }
    }
}
