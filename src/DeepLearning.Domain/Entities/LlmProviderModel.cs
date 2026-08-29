using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    /// <summary>
    /// A known model for a given provider (e.g. provider_key="claude", model="claude-sonnet-5").
    /// This is the single source of truth for both "which models are known for this provider"
    /// and "which one is currently in effect" — <see cref="IsCurrent"/> marks the latter, and a
    /// partial unique index guarantees at most one IsCurrent=true row per ProviderKey. There is
    /// deliberately no separate "current model" column anywhere else (e.g. on
    /// <see cref="LlmProviderSettings"/>) — a second copy of the same fact is how it drifts.
    /// Adding a row here (a new known model) never changes what's currently running; only
    /// flipping IsCurrent does that.
    /// </summary>
    public class LlmProviderModel : Entity
    {
        public string ProviderKey { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? Label { get; set; }
        public bool IsCurrent { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
