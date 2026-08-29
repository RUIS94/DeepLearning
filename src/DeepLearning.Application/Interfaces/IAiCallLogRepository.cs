using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    public interface IAiCallLogRepository
    {
        Task AddAsync(AiCallLog log, CancellationToken cancellationToken = default);
    }
}
