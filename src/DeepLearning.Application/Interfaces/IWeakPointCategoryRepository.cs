using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    /// <summary>The fixed, global 8-row top-level taxonomy (seed data only — nothing mints a row at runtime).</summary>
    public interface IWeakPointCategoryRepository
    {
        Task<WeakPointCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<WeakPointCategory?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

        Task<List<WeakPointCategory>> ListAllAsync(CancellationToken cancellationToken = default);
    }
}
