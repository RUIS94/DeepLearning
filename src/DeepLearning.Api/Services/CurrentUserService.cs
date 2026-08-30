using DeepLearning.Application.Interfaces;

namespace DeepLearning.Api.Services
{
    /// <summary>
    /// Reads the authenticated caller's identity off HttpContext.User — populated by the JwtBearer
    /// handler from a validated Supabase-issued token's "sub"/"email" claims (Program.cs sets
    /// MapInboundClaims = false so these keep their original JWT names instead of being remapped
    /// to the long ClaimTypes.* URIs). Lives in Api, not Infrastructure, because IHttpContextAccessor
    /// is an ASP.NET Core hosting concept — same layering precedent as GlobalExceptionHandler/
    /// CorrelationIdMiddleware.
    /// </summary>
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid? UserId
        {
            get
            {
                var sub = _httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;
                return Guid.TryParse(sub, out var userId) ? userId : null;
            }
        }

        public string? Email => _httpContextAccessor.HttpContext?.User.FindFirst("email")?.Value;
    }
}
