using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    public interface IReviewLibraryRepository
    {
        Task<List<SentencePattern>> GetPatternsByQuestionIdAsync(Guid questionId, CancellationToken cancellationToken = default);

        Task<List<VocabExpression>> GetVocabByQuestionIdAsync(Guid questionId, CancellationToken cancellationToken = default);

        Task<SentencePattern?> GetPatternByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<VocabExpression?> GetVocabByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cross-question browsing (design doc §2.2) — every accumulated pattern, optionally
        /// filtered by domain/scenario/frequency tag, regardless of which question it came from.
        /// </summary>
        Task<List<SentencePattern>> ListPatternsAsync(
            string? domain, string? scenario, string? frequencyTag, CancellationToken cancellationToken = default);

        Task<List<VocabExpression>> ListVocabAsync(
            string? domain, string? scenario, string? frequencyTag, CancellationToken cancellationToken = default);

        Task<UserPatternReview?> GetUserPatternReviewAsync(Guid userId, Guid patternId, CancellationToken cancellationToken = default);

        Task<UserVocabReview?> GetUserVocabReviewAsync(Guid userId, Guid vocabId, CancellationToken cancellationToken = default);

        /// <summary>One user's review state for a batch of patterns — used to overlay per-user mastery onto the cross-question browse list without one query per item.</summary>
        Task<List<UserPatternReview>> ListUserPatternReviewsAsync(Guid userId, IEnumerable<Guid> patternIds, CancellationToken cancellationToken = default);

        Task<List<UserVocabReview>> ListUserVocabReviewsAsync(Guid userId, IEnumerable<Guid> vocabIds, CancellationToken cancellationToken = default);

        Task AddUserPatternReviewAsync(UserPatternReview review, CancellationToken cancellationToken = default);

        Task AddUserVocabReviewAsync(UserVocabReview review, CancellationToken cancellationToken = default);

        Task AddPatternsAsync(IEnumerable<SentencePattern> patterns, CancellationToken cancellationToken = default);

        Task AddVocabAsync(IEnumerable<VocabExpression> vocab, CancellationToken cancellationToken = default);
    }
}
