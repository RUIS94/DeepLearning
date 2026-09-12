using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    public interface IUserFeatureOverrideRepository
    {
        Task<UserFeatureOverride?> GetAsync(Guid userId, string featureKey, CancellationToken cancellationToken = default);

        Task<List<UserFeatureOverride>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>Upsert. Does not call SaveChanges — the handler owns the unit of work.</summary>
        Task SetAsync(Guid userId, string featureKey, bool enabled, CancellationToken cancellationToken = default);

        /// <summary>Removes the override so the key falls back to the global flag. No-op if absent.</summary>
        Task ClearAsync(Guid userId, string featureKey, CancellationToken cancellationToken = default);
    }
}
