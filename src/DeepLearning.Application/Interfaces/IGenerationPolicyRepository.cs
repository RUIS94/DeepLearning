using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    public interface IGenerationPolicyRepository
    {
        Task<GenerationPolicy?> GetByKeyAsync(Guid examTypeId, string policyKey, CancellationToken cancellationToken = default);
    }
}
