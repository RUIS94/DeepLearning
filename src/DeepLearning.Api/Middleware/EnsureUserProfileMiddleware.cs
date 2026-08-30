using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Api.Middleware
{
    /// <summary>
    /// Registration/login themselves happen entirely against Supabase Auth — this backend never
    /// issues or validates credentials, only JWTs. The first time a validated JWT's "sub" shows up
    /// with no matching public.users row, this creates one from the token's own claims (email,
    /// falling back to a username derived from it) so FK-constrained tables (submissions,
    /// weak_points, etc.) have somewhere to point. No-op — zero DB access — for unauthenticated
    /// requests and for a "sub" that already has a profile row, so this costs nothing on the
    /// overwhelming majority of requests once a user's first call has synced them.
    /// </summary>
    public class EnsureUserProfileMiddleware
    {
        private readonly RequestDelegate _next;

        public EnsureUserProfileMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.User.Identity?.IsAuthenticated == true
                && Guid.TryParse(context.User.FindFirst("sub")?.Value, out var userId))
            {
                var userRepository = context.RequestServices.GetRequiredService<IUserRepository>();
                var existing = await userRepository.GetByIdAsync(userId, context.RequestAborted);

                if (existing is null)
                {
                    var email = context.User.FindFirst("email")?.Value ?? $"{userId:N}@supabase.local";
                    var user = new User
                    {
                        Id = userId,
                        Username = DeriveUsername(email, userId),
                        Email = email,
                        // PasswordHash is deliberately left empty — Supabase Auth owns credentials
                        // now, this column is vestigial (kept per the "don't drop columns" migration
                        // discipline, see AGENTS.md).
                        CreatedAt = DateTimeOffset.UtcNow,
                    };

                    try
                    {
                        await userRepository.AddAsync(user, context.RequestAborted);
                        var unitOfWork = context.RequestServices.GetRequiredService<IUnitOfWork>();
                        await unitOfWork.SaveChangesAsync(context.RequestAborted);
                    }
                    catch (DbUpdateException)
                    {
                        // A concurrent request for the same brand-new Supabase user won the race
                        // and already inserted the profile row (unique index on username/email) —
                        // not an error, just proceed without needing our own copy of it.
                    }
                }
            }

            await _next(context);
        }

        private static string DeriveUsername(string email, Guid userId)
        {
            var atIndex = email.IndexOf('@');
            var localPart = atIndex > 0 ? email[..atIndex] : string.Empty;
            return string.IsNullOrWhiteSpace(localPart) ? $"user_{userId:N}" : localPart;
        }
    }
}
