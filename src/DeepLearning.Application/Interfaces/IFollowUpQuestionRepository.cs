using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    public interface IFollowUpQuestionRepository
    {
        Task<FollowUpQuestion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task AddAsync(FollowUpQuestion followUpQuestion, CancellationToken cancellationToken = default);
    }
}
