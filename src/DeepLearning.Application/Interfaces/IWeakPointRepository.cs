using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Interfaces
{
    public interface IWeakPointRepository
    {
        /// <summary>Cap on how many weak points the grading prompt injects — a fixed-size block regardless of history depth (design decision: K = 6).</summary>
        public const int GradingPromptWeakPointLimit = 6;

        Task<WeakPoint?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>Legacy-bucket lookup — only rows with <c>catalog_id IS NULL</c> use <see cref="WeakPoint.Category"/>.</summary>
        Task<WeakPoint?> GetByUserAndCategoryAsync(Guid userId, string category, CancellationToken cancellationToken = default);

        /// <summary>All users' weak points mapped to one catalog kind — the merge / reclassify surface.</summary>
        Task<List<WeakPoint>> ListByCatalogIdAsync(Guid catalogId, CancellationToken cancellationToken = default);

        Task<List<WeakPointOccurrence>> ListOccurrencesByWeakPointAsync(Guid weakPointId, CancellationToken cancellationToken = default);

        /// <summary>Same as <see cref="ListOccurrencesByWeakPointAsync"/> but with each occurrence's <see cref="WeakPointOccurrence.ErrorList"/> eager-loaded — the weak_point_detection_criteria call's historical-evidence input needs the error's full explanation, not just the occurrence's snippet copy.</summary>
        Task<List<WeakPointOccurrence>> ListOccurrencesWithErrorByWeakPointAsync(Guid weakPointId, CancellationToken cancellationToken = default);

        void RemoveWeakPoint(WeakPoint weakPoint);

        void RemoveOccurrence(WeakPointOccurrence occurrence);

        /// <summary>Lookup for the catalog-based path — one row per (user, catalog entry).</summary>
        Task<WeakPoint?> GetByUserAndCatalogAsync(Guid userId, Guid catalogId, CancellationToken cancellationToken = default);

        Task<List<WeakPoint>> ListByUserAsync(Guid userId, WeakPointStatus? status, CancellationToken cancellationToken = default);

        /// <summary>
        /// Active weak points for a user with <see cref="WeakPoint.Catalog"/> eager-loaded,
        /// ordered high-priority first then most-recently-seen. <paramref name="limit"/> caps the
        /// result (grading prompt passes <see cref="Persistence.Repositories.WeakPointRepository.GradingPromptLimit"/>;
        /// the classifier pass passes null to get the full active set for summary merging).
        /// </summary>
        Task<List<WeakPoint>> ListActiveWithCatalogByUserAsync(Guid userId, int? limit = null, CancellationToken cancellationToken = default);

        /// <summary>True if an occurrence already exists for this (weak point, submission) — re-grade / concurrent-event guard.</summary>
        Task<bool> OccurrenceExistsAsync(Guid weakPointId, Guid submissionId, CancellationToken cancellationToken = default);

        /// <summary>An existing catalog entry for this code, or null. Used before minting a proposed one.</summary>
        Task<WeakPointCatalog?> GetCatalogByCodeAsync(string code, CancellationToken cancellationToken = default);

        Task AddAsync(WeakPoint weakPoint, CancellationToken cancellationToken = default);

        Task AddCatalogAsync(WeakPointCatalog catalog, CancellationToken cancellationToken = default);

        Task AddOccurrenceAsync(WeakPointOccurrence occurrence, CancellationToken cancellationToken = default);
    }
}
