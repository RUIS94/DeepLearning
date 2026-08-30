using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Interfaces
{
    public interface IWeakPointRepository
    {
        Task<WeakPoint?> GetByUserAndCategoryAsync(Guid userId, string category, CancellationToken cancellationToken = default);

        Task<List<WeakPoint>> ListByUserAsync(Guid userId, WeakPointStatus? status, CancellationToken cancellationToken = default);

        Task AddAsync(WeakPoint weakPoint, CancellationToken cancellationToken = default);

        Task AddOccurrenceAsync(WeakPointOccurrence occurrence, CancellationToken cancellationToken = default);
    }
}
