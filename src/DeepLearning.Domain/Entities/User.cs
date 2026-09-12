using DeepLearning.Domain.Common;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Domain.Entities
{
    public class User : AggregateRoot
    {
        /// <summary>
        /// Defaults to `user` for every row — admin is granted explicitly, never inferred.
        /// EnsureUserProfileMiddleware mirrors this into a ClaimTypes.Role claim on every request
        /// so [Authorize(Roles = "admin")]/the "AdminOnly" policy see it fresh (no re-login needed
        /// after a role change), instead of trusting a Supabase JWT claim that would go stale.
        /// </summary>
        public UserRole Role { get; set; } = UserRole.user;

        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Vestigial — Supabase Auth owns credentials now (see Auth section in AGENTS.md).
        /// Left null for every user created after the Supabase Auth switchover; kept as a column
        /// (nullable, not dropped) per this project's own "migrations only add, never remove"
        /// discipline. Pre-Supabase rows may still carry an old PBKDF2 hash here, but nothing
        /// reads or writes it anymore.
        /// </summary>
        public string? PasswordHash { get; set; }

        public string? DisplayName { get; set; }

        /// <summary>
        /// Which language the web UI renders in for this user: "en" (default) or "zh". This is a
        /// pure front-end display preference — it does not affect any stored content, which may
        /// still be Chinese regardless of this value.
        /// </summary>
        public string LanguagePreference { get; set; } = "en";

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? LastLoginAt { get; set; }
    }
}
