using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    public class FeatureFlag : Entity
    {
        public string Key { get; set; } = string.Empty;
        public bool Enabled { get; set; }
        public string Scope { get; set; } = "global";
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
