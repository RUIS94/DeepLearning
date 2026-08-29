using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    public class User : AggregateRoot
    {
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? LastLoginAt { get; set; }
    }
}
