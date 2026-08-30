using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    public interface IReviewLibraryRepository
    {
        Task<List<SentencePattern>> GetPatternsByQuestionIdAsync(Guid questionId, CancellationToken cancellationToken = default);

        Task<List<VocabExpression>> GetVocabByQuestionIdAsync(Guid questionId, CancellationToken cancellationToken = default);

        Task<UserPatternReview?> GetUserPatternReviewAsync(Guid userId, Guid patternId, CancellationToken cancellationToken = default);

        Task<UserVocabReview?> GetUserVocabReviewAsync(Guid userId, Guid vocabId, CancellationToken cancellationToken = default);

        Task AddUserPatternReviewAsync(UserPatternReview review, CancellationToken cancellationToken = default);

        Task AddUserVocabReviewAsync(UserVocabReview review, CancellationToken cancellationToken = default);

        Task AddPatternsAsync(IEnumerable<SentencePattern> patterns, CancellationToken cancellationToken = default);

        Task AddVocabAsync(IEnumerable<VocabExpression> vocab, CancellationToken cancellationToken = default);
    }
}
