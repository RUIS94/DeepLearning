using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Interfaces
{
    public interface IQuestionRepository
    {
        Task<Question?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<List<Question>> ListAsync(
            TaskType? taskType,
            Difficulty? difficulty,
            bool? inBank,
            CancellationToken cancellationToken = default);

        Task<List<MeaningCheckpoint>> GetMeaningCheckpointsAsync(Guid questionId, CancellationToken cancellationToken = default);

        Task<List<TaskBSeededError>> GetSeededErrorsAsync(Guid questionId, CancellationToken cancellationToken = default);

        Task AddAsync(Question question, CancellationToken cancellationToken = default);

        Task AddMeaningCheckpointsAsync(IEnumerable<MeaningCheckpoint> checkpoints, CancellationToken cancellationToken = default);

        Task AddSeededErrorsAsync(IEnumerable<TaskBSeededError> seededErrors, CancellationToken cancellationToken = default);
    }
}
