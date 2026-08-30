using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Events
{
    /// <summary>
    /// Raised by StandardOverride when its status flips to 'active' — either the automatic
    /// count-based path (StandardOverrideActivationPolicy, design doc §10.6) or the manual
    /// ActivateStandardOverride command. No subscriber exists yet; wiring this now (rather than
    /// leaving the event class empty) means a future consumer (e.g. invalidating a cached rubric
    /// summary, or feeding generation_policy) is a new EventHandler, not a change to either
    /// activation path.
    /// </summary>
    public class StandardOverrideActivatedEvent
    {
        public Guid StandardOverrideId { get; init; }
        public OverrideScope Scope { get; init; }
        public string DimensionOrRule { get; init; } = string.Empty;
        public Guid? PreviousOverrideId { get; init; }
        public DateTimeOffset ActivatedAt { get; init; }
    }
}
