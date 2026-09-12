using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    /// <summary>
    /// Per-user override of a `feature_flags` key (ref/管理员与用户权限隔离_策划书.md A4) — lets an
    /// admin grant or withhold one feature for a specific user independent of the global flag.
    /// FeatureGateAttribute checks this first; absent a row, the global FeatureFlag.Enabled wins.
    /// </summary>
    public class UserFeatureOverride : Entity
    {
        public Guid UserId { get; set; }

        public string FeatureKey { get; set; } = string.Empty;

        public bool Enabled { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public User? User { get; set; }
    }
}
