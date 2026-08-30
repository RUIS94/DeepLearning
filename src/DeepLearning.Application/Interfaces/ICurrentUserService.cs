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
    }
}
