namespace DeepLearning.Application.Interfaces
{
    /// <summary>
    /// Reads <c>feature_flags</c> with a short in-process cache so a gated endpoint doesn't turn
    /// into a DB round trip per request. Returns <see cref="Common.FeatureFlags"/>' documented
    /// default when a key has no row (a fresh DB behaves as it did pre-flags).
    /// </summary>
    public interface IFeatureFlagService
    {
        Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken = default);
    }
}
