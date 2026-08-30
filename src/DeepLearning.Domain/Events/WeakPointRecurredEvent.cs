namespace DeepLearning.Domain.Events
{
    /// <summary>
    /// Raised by WeakPoint when a previously 'resolved' weak point is seen again (design doc
    /// §10.4 — "学会了又忘了" vs. "从未真正学会" are different signals, so this is distinct from
    /// the ordinary still-active-and-seen-again case, which does not raise this event). No
    /// subscriber exists yet — kept as a hook for a future consumer (e.g. surfacing recurrence in
    /// generation_policy's weak-point-targeted question selection, design doc §10.5).
    /// </summary>
    public class WeakPointRecurredEvent
    {
        public Guid WeakPointId { get; init; }
        public Guid UserId { get; init; }
        public string Category { get; init; } = string.Empty;
        public int RecurrenceCount { get; init; }
        public DateTimeOffset RecurredAt { get; init; }
    }
}
