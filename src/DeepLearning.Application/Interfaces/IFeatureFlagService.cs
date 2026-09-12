namespace DeepLearning.Application.Interfaces
{
    /// <summary>
    /// Reads <c>feature_flags</c> with a short in-process cache so a gated endpoint doesn't turn
    /// into a DB round trip per request. Returns <see cref="Common.FeatureFlags"/>' documented
    /// default when a key has no row (a fresh DB behaves as it did pre-flags).
    /// </summary>
    public interface IFeatureFlagService
    {
        /// <summary>
        /// When <paramref name="userId"/> is given, a matching UserFeatureOverride row wins over
        /// the global flag (ref/管理员与用户权限隔离_策划书.md A4); omitted, this is the plain
        /// global check every internal/system caller used before per-user overrides existed.
        /// </summary>
        Task<bool> IsEnabledAsync(string key, Guid? userId = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Drops the cached value for <paramref name="key"/> so the next read hits the DB — called
        /// by the set-flag command so a toggle from the settings UI takes effect immediately
        /// rather than after the cache TTL.
        /// </summary>
        void Invalidate(string key);
    }
}
