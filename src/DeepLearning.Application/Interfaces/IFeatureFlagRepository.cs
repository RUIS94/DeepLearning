using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    public interface IFeatureFlagRepository
    {
        Task<FeatureFlag?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

        Task<List<FeatureFlag>> ListAsync(CancellationToken cancellationToken = default);
    }
}
