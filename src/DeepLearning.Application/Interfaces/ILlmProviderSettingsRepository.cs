using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    public interface ILlmProviderSettingsRepository
    {
        Task<LlmProviderSettings?> GetActiveAsync(CancellationToken cancellationToken = default);

        Task<LlmProviderSettings?> GetByProviderKeyAsync(string providerKey, CancellationToken cancellationToken = default);

        Task<List<LlmProviderSettings>> ListAsync(CancellationToken cancellationToken = default);
    }
}
