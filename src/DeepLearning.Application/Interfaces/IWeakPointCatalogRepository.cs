using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    /// <summary>
    /// The curated <see cref="WeakPointCatalog"/> reference data. Runtime rule/AI matching reads
    /// <see cref="ListByExamTypeAsync"/> (proposed + active, never deprecated). The admin surface
    /// reads <see cref="ListAllByExamTypeAsync"/> and writes via the create / update / merge
    /// commands — proposed rows minted at runtime or by an admin are curated here.
    /// </summary>
    public interface IWeakPointCatalogRepository
    {
        /// <summary>Kinds eligible for matching — <c>proposed</c> + <c>active</c>, excludes <c>deprecated</c>.</summary>
        Task<List<WeakPointCatalog>> ListByExamTypeAsync(Guid examTypeId, CancellationToken cancellationToken = default);

        /// <summary>Every kind for the exam type regardless of status — the admin view.</summary>
        Task<List<WeakPointCatalog>> ListAllByExamTypeAsync(Guid examTypeId, CancellationToken cancellationToken = default);

        Task<WeakPointCatalog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(Guid examTypeId, string code, CancellationToken cancellationToken = default);

        Task AddAsync(WeakPointCatalog catalog, CancellationToken cancellationToken = default);
    }
}
