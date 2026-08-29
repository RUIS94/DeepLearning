using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    public interface ILlmProviderModelRepository
    {
        Task AddAsync(LlmProviderModel model, CancellationToken cancellationToken = default);

        Task<LlmProviderModel?> GetByProviderKeyAndModelAsync(string providerKey, string model, CancellationToken cancellationToken = default);

        Task<LlmProviderModel?> GetCurrentAsync(string providerKey, CancellationToken cancellationToken = default);

        Task<List<LlmProviderModel>> ListByProviderKeyAsync(string providerKey, CancellationToken cancellationToken = default);

        Task<List<LlmProviderModel>> ListCurrentAsync(CancellationToken cancellationToken = default);
    }
}
