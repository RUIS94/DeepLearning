using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    public interface IUserExamTypeActivationRepository
    {
        Task<UserExamTypeActivation?> GetAsync(Guid userId, Guid examTypeId, CancellationToken cancellationToken = default);

        Task<List<UserExamTypeActivation>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>Upsert. Does not call SaveChanges — the handler owns the unit of work.</summary>
        Task SetActiveAsync(Guid userId, Guid examTypeId, bool isActive, CancellationToken cancellationToken = default);
    }
}
