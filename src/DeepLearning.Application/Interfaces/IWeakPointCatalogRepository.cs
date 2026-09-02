using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    /// <summary>
    /// Read access to the curated <see cref="WeakPointCatalog"/> reference data. Insert-only
    /// (seeded by seed_weak_point_catalog_naati_ct.sql), so no write methods here — new kinds
    /// are added via a data migration, same discipline as assessment_dimensions / error_taxonomies.
    /// </summary>
    public interface IWeakPointCatalogRepository
    {
        Task<List<WeakPointCatalog>> ListByExamTypeAsync(Guid examTypeId, CancellationToken cancellationToken = default);
    }
}
