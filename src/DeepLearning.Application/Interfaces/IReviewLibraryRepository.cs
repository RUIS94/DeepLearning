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

        // --- vocab_glossary: the canonical cross-question entry (one per distinct expression) ---

        Task<VocabGlossaryEntry?> GetGlossaryEntryByCanonicalKeyAsync(string canonicalKey, CancellationToken cancellationToken = default);

        Task<VocabGlossaryEntry?> GetGlossaryEntryByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task AddGlossaryEntryAsync(VocabGlossaryEntry entry, CancellationToken cancellationToken = default);

        /// <summary>Cross-question browsing for the review library — canonical entries, optionally filtered.</summary>
        Task<List<VocabGlossaryEntry>> ListGlossaryAsync(
            string? domain, string? scenario, string? frequencyTag, CancellationToken cancellationToken = default);

        Task<List<VocabGlossaryEntry>> ListGlossaryEntriesByCanonicalKeysAsync(
            IEnumerable<string> canonicalKeys, CancellationToken cancellationToken = default);

        /// <summary>
        /// This question's <see cref="VocabExpression"/> snapshots whose <see cref="VocabExpression.CanonicalKey"/>
        /// occurs on more than one question overall — i.e. the expressions in this passage that
        /// have been seen before and are candidates for a <c>vocab_semantic_drift</c> check.
        /// </summary>
        Task<List<VocabExpression>> ListRecurringVocabForQuestionAsync(Guid questionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Already-accumulated vocab whose <see cref="VocabExpression.CanonicalKey"/> literally
        /// appears in <paramref name="sourceText"/> — i.e. an expression captured from an earlier
        /// question that recurs in this one. Most recent first, capped at <paramref name="take"/>.
        ///
        /// <para>Currently unused: it used to feed the deep-learning prompt, but that inline
        /// cross-question block was removed (dedup belongs in the backend on canonical_key, and
        /// re-explaining a recurring term should be its own small AI call). Kept as the query the
        /// planned standalone "enrich recurring term" call will build on.</para>
        /// </summary>
        Task<List<VocabExpression>> ListPriorVocabForSourceAsync(string sourceText, int take, CancellationToken cancellationToken = default);

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
