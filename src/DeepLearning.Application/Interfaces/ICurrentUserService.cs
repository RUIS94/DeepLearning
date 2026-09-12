using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Interfaces
{
    /// <summary>
    /// The identity of the caller, derived from a validated Supabase-issued JWT (the "sub"/"email"
    /// claims) when one is present on the request. Both members are null when the request carries
    /// no valid JWT — every controller that reads this treats that as "fall back to whatever
    /// UserId the caller passed explicitly in the request body/query," not as an error, since
    /// authentication is opt-in for now (see AGENTS.md's Auth section).
    /// </summary>
    public interface ICurrentUserService
    {
        Guid? UserId { get; }

        string? Email { get; }

        /// <summary>
        /// Read off the ClaimTypes.Role claim EnsureUserProfileMiddleware attaches from the
        /// caller's own `public.users` row (not from the Supabase JWT itself, which would go
        /// stale until re-login). Null when unauthenticated.
        /// </summary>
        UserRole? Role { get; }

        bool IsAdmin => Role == UserRole.admin;

        /// <summary>
        /// UserId, guaranteed non-null once the global [Authorize] requirement (Program.cs) is in
        /// force — every controller action reachable at all has already been authenticated by the
        /// time it runs. Throws instead of silently defaulting to Guid.Empty if that invariant is
        /// ever violated (e.g. an endpoint carrying its own [AllowAnonymous]).
        /// </summary>
        Guid RequiredUserId => UserId ?? throw new UnauthorizedAccessException("No authenticated user on this request.");
    }
}
