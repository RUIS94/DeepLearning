using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Interfaces
{
    public interface IWeakPointRepository
    {
        Task<WeakPoint?> GetByUserAndCategoryAsync(Guid userId, string category, CancellationToken cancellationToken = default);

        /// <summary>Lookup for the catalog-based path — one row per (user, catalog entry).</summary>
        Task<WeakPoint?> GetByUserAndCatalogAsync(Guid userId, Guid catalogId, CancellationToken cancellationToken = default);

        Task<List<WeakPoint>> ListByUserAsync(Guid userId, WeakPointStatus? status, CancellationToken cancellationToken = default);

        /// <summary>
        /// Active weak points for a user with <see cref="WeakPoint.Catalog"/> eager-loaded,
        /// ordered high-priority first then most-recently-seen — the shape GradeSubmissionCommandHandler
        /// injects into the grading prompt.
        /// </summary>
        Task<List<WeakPoint>> ListActiveWithCatalogByUserAsync(Guid userId, CancellationToken cancellationToken = default);

        Task AddAsync(WeakPoint weakPoint, CancellationToken cancellationToken = default);

        Task AddOccurrenceAsync(WeakPointOccurrence occurrence, CancellationToken cancellationToken = default);
    }
}
