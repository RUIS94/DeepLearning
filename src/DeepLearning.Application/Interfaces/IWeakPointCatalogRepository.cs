using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    /// <summary>
    /// The curated <see cref="WeakPointCatalog"/> reference data — globally shared, not scoped by
    /// exam type. Runtime rule/AI matching reads <see cref="ListActiveAsync"/> (proposed + active,
    /// never deprecated). The admin surface reads <see cref="ListAllAsync"/> and writes via the
    /// create / update / merge commands — proposed rows minted at runtime or by an admin are
    /// curated here.
    /// </summary>
    public interface IWeakPointCatalogRepository
    {
        /// <summary>Kinds eligible for matching — <c>proposed</c> + <c>active</c>, excludes <c>deprecated</c>.</summary>
        Task<List<WeakPointCatalog>> ListActiveAsync(CancellationToken cancellationToken = default);

        /// <summary>Every kind regardless of status — the admin view.</summary>
        Task<List<WeakPointCatalog>> ListAllAsync(CancellationToken cancellationToken = default);

        Task<WeakPointCatalog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<WeakPointCatalog?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(string code, CancellationToken cancellationToken = default);

        Task AddAsync(WeakPointCatalog catalog, CancellationToken cancellationToken = default);
    }
}
