using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    public interface IFeatureFlagRepository
    {
        Task<FeatureFlag?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

        Task<List<FeatureFlag>> ListAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Upsert: sets <paramref name="key"/>'s <c>enabled</c> (+ <c>updated_at</c>), inserting a
        /// <c>scope = 'global'</c> row if none exists. Does not call SaveChanges — the handler owns
        /// the unit of work.
        /// </summary>
        Task SetEnabledAsync(string key, bool enabled, CancellationToken cancellationToken = default);
    }
}
