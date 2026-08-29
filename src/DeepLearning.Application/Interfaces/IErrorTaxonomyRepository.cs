using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    public interface IErrorTaxonomyRepository
    {
        Task<ErrorTaxonomy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<List<ErrorTaxonomy>> ListByExamTypeAsync(Guid examTypeId, CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(Guid examTypeId, string categoryKey, CancellationToken cancellationToken = default);

        Task AddAsync(ErrorTaxonomy taxonomy, CancellationToken cancellationToken = default);
    }
}
