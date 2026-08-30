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
            Guid? categoryId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Candidate real-exam samples (IsSeedReference=true) for few-shot question generation —
        /// filtered by task type + optional difficulty/category, most-recent first, capped at
        /// <paramref name="take"/>. Simple relational filtering only (design doc §11.2 Step 8:
        /// "先按领域/难度过滤,pgvector语义检索作为后续优化") — no semantic ranking.
        /// </summary>
        Task<List<Question>> ListSeedReferenceCandidatesAsync(
            TaskType taskType,
            Difficulty? difficulty,
            Guid? categoryId,
            int take,
            CancellationToken cancellationToken = default);

        /// <summary>Fetches a specific set of questions by id — used to look up caller-specified seed references (bypassing ListSeedReferenceCandidatesAsync's automatic filtering) rather than a general-purpose bulk-get.</summary>
        Task<List<Question>> ListByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

        Task<List<MeaningCheckpoint>> GetMeaningCheckpointsAsync(Guid questionId, CancellationToken cancellationToken = default);

        Task<List<TaskBSeededError>> GetSeededErrorsAsync(Guid questionId, CancellationToken cancellationToken = default);

        Task<List<Guid>> ListCategoryIdsAsync(Guid questionId, CancellationToken cancellationToken = default);

        Task<bool> HasCategoryMapAsync(Guid questionId, Guid categoryId, CancellationToken cancellationToken = default);

        Task AddAsync(Question question, CancellationToken cancellationToken = default);

        Task AddMeaningCheckpointsAsync(IEnumerable<MeaningCheckpoint> checkpoints, CancellationToken cancellationToken = default);

        Task AddSeededErrorsAsync(IEnumerable<TaskBSeededError> seededErrors, CancellationToken cancellationToken = default);

        Task AddCategoryMapAsync(QuestionCategoryMap categoryMap, CancellationToken cancellationToken = default);
    }
}
