using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Entities
{
    public class StandardOverride : AggregateRoot
    {
        public OverrideScope Scope { get; set; }
        public string DimensionOrRule { get; set; } = string.Empty;
        public string? OriginalRuleText { get; set; }
        public string RevisedRuleText { get; set; } = string.Empty;
        public Guid? TriggeredByFollowupId { get; set; }
        public OverrideStatus Status { get; set; } = OverrideStatus.observing;
        public Guid? PreviousOverrideId { get; set; }
        public DateTimeOffset? EffectiveFrom { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public FollowUpQuestion? TriggeredByFollowup { get; set; }
        public StandardOverride? PreviousOverride { get; set; }
    }
}
