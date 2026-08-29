using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Entities
{
    public class AiCallLog : Entity
    {
        public AiOperationType RequestType { get; set; }
        public Guid? RelatedId { get; set; }
        public CallStatus Status { get; set; } = CallStatus.pending;
        public int AttemptCount { get; set; }
        public int MaxRetries { get; set; } = 3;
        public string? LastErrorMessage { get; set; }
        public int? LatencyMs { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? ResolvedAt { get; set; }
    }
}
