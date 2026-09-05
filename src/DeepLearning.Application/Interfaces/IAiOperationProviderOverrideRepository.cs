using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Interfaces
{
    public interface IAiOperationProviderOverrideRepository
    {
        Task<AiOperationProviderOverride?> GetByOperationTypeAsync(
            AiOperationType operationType, CancellationToken cancellationToken = default);

        Task<List<AiOperationProviderOverride>> ListAsync(CancellationToken cancellationToken = default);

        Task AddAsync(AiOperationProviderOverride entity, CancellationToken cancellationToken = default);

        void Remove(AiOperationProviderOverride entity);
    }
}
