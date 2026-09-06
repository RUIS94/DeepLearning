using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class ReviewLibraryRepository : IReviewLibraryRepository
    {
        private readonly AppDbContext _context;

        public ReviewLibraryRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<List<SentencePattern>> GetPatternsByQuestionIdAsync(Guid questionId, CancellationToken cancellationToken = default)
            => _context.SentencePatterns.Where(x => x.QuestionId == questionId).ToListAsync(cancellationToken);

        public Task<List<VocabExpression>> GetVocabByQuestionIdAsync(Guid questionId, CancellationToken cancellationToken = default)
            => _context.VocabExpressions.Where(x => x.QuestionId == questionId).ToListAsync(cancellationToken);

        public Task<SentencePattern?> GetPatternByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.SentencePatterns.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public Task<VocabExpression?> GetVocabByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.VocabExpressions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public Task<List<SentencePattern>> ListPatternsAsync(
            string? domain, string? scenario, string? frequencyTag, CancellationToken cancellationToken = default)
        {
            var query = _context.SentencePatterns.AsQueryable();

            if (!string.IsNullOrEmpty(domain))
            {
                query = query.Where(x => x.Domain == domain);
            }

            if (!string.IsNullOrEmpty(scenario))
            {
                query = query.Where(x => x.Scenario == scenario);
            }

            if (!string.IsNullOrEmpty(frequencyTag))
            {
                query = query.Where(x => x.FrequencyTag == frequencyTag);
            }

            return query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        }

        public Task<List<VocabExpression>> ListVocabAsync(
            string? domain, string? scenario, string? frequencyTag, CancellationToken cancellationToken = default)
        {
            var query = _context.VocabExpressions.AsQueryable();

            if (!string.IsNullOrEmpty(domain))
            {
                query = query.Where(x => x.Domain == domain);
            }

            if (!string.IsNullOrEmpty(scenario))
            {
                query = query.Where(x => x.Scenario == scenario);
            }

            if (!string.IsNullOrEmpty(frequencyTag))
            {
                query = query.Where(x => x.FrequencyTag == frequencyTag);
            }

            return query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        }

        public Task<VocabGlossaryEntry?> GetGlossaryEntryByCanonicalKeyAsync(string canonicalKey, CancellationToken cancellationToken = default)
            => _context.VocabGlossary.FirstOrDefaultAsync(x => x.CanonicalKey == canonicalKey, cancellationToken);

        public Task<VocabGlossaryEntry?> GetGlossaryEntryByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.VocabGlossary.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public async Task AddGlossaryEntryAsync(VocabGlossaryEntry entry, CancellationToken cancellationToken = default)
            => await _context.VocabGlossary.AddAsync(entry, cancellationToken);

        public Task<List<VocabGlossaryEntry>> ListGlossaryAsync(
            string? domain, string? scenario, string? frequencyTag, CancellationToken cancellationToken = default)
        {
            var query = _context.VocabGlossary.AsQueryable();

            if (!string.IsNullOrEmpty(domain))
            {
                query = query.Where(x => x.Domain == domain);
            }

            if (!string.IsNullOrEmpty(scenario))
            {
                query = query.Where(x => x.Scenario == scenario);
            }

            if (!string.IsNullOrEmpty(frequencyTag))
            {
                query = query.Where(x => x.FrequencyTag == frequencyTag);
            }

            return query.OrderByDescending(x => x.UpdatedAt).ToListAsync(cancellationToken);
        }

        public Task<List<VocabGlossaryEntry>> ListGlossaryEntriesByCanonicalKeysAsync(
            IEnumerable<string> canonicalKeys, CancellationToken cancellationToken = default)
        {
            var keys = canonicalKeys.Distinct().ToList();
            if (keys.Count == 0)
            {
                return Task.FromResult(new List<VocabGlossaryEntry>());
            }

            return _context.VocabGlossary.Where(x => keys.Contains(x.CanonicalKey)).ToListAsync(cancellationToken);
        }

        public async Task<List<VocabExpression>> ListRecurringVocabForQuestionAsync(Guid questionId, CancellationToken cancellationToken = default)
        {
            // canonical_key values that appear on more than one question overall.
            var recurringKeys = await _context.VocabExpressions
                .Where(x => x.CanonicalKey != null)
                .GroupBy(x => x.CanonicalKey)
                .Where(g => g.Select(x => x.QuestionId).Distinct().Count() > 1)
                .Select(g => g.Key)
                .ToListAsync(cancellationToken);

            if (recurringKeys.Count == 0)
            {
                return [];
            }

            return await _context.VocabExpressions
                .Where(x => x.QuestionId == questionId && x.CanonicalKey != null && recurringKeys.Contains(x.CanonicalKey))
                .ToListAsync(cancellationToken);
        }

        public async Task<List<VocabExpression>> ListPriorVocabForSourceAsync(string sourceText, int take, CancellationToken cancellationToken = default)
        {
            // "does canonical_key appear as a substring of the source text" is a
            // parameter-on-the-left LIKE that not every provider translates cleanly — and the
            // review library stays small for a single learner — so pull a lightweight
            // (id, key, createdAt) projection and do the substring test in memory, then load
            // the matched rows by id. canonical_key is stored lower-cased; lower-case the
            // source to match. Keys shorter than 3 chars would match almost anything.
            var loweredSource = sourceText.ToLowerInvariant();

            var candidates = await _context.VocabExpressions
                .Where(x => x.CanonicalKey != null && x.CanonicalKey.Length >= 3)
                .Select(x => new { x.Id, x.CanonicalKey, x.CreatedAt })
                .ToListAsync(cancellationToken);

            var matchedIds = candidates
                .Where(c => loweredSource.Contains(c.CanonicalKey!, StringComparison.Ordinal))
                .OrderByDescending(c => c.CreatedAt)
                .Take(take)
                .Select(c => c.Id)
                .ToList();

            if (matchedIds.Count == 0)
            {
                return [];
            }

            var rows = await _context.VocabExpressions
                .Where(x => matchedIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            var rank = matchedIds.Select((id, index) => (id, index)).ToDictionary(t => t.id, t => t.index);
            return rows.OrderBy(r => rank[r.Id]).ToList();
        }

        public Task<List<UserPatternReview>> ListUserPatternReviewsAsync(Guid userId, IEnumerable<Guid> patternIds, CancellationToken cancellationToken = default)
            => _context.UserPatternReview.Where(x => x.UserId == userId && patternIds.Contains(x.PatternId)).ToListAsync(cancellationToken);

        public Task<List<UserVocabReview>> ListUserVocabReviewsAsync(Guid userId, IEnumerable<Guid> vocabIds, CancellationToken cancellationToken = default)
            => _context.UserVocabReview.Where(x => x.UserId == userId && vocabIds.Contains(x.VocabId)).ToListAsync(cancellationToken);

        public Task<UserPatternReview?> GetUserPatternReviewAsync(Guid userId, Guid patternId, CancellationToken cancellationToken = default)
            => _context.UserPatternReview.FirstOrDefaultAsync(x => x.UserId == userId && x.PatternId == patternId, cancellationToken);

        public Task<UserVocabReview?> GetUserVocabReviewAsync(Guid userId, Guid vocabId, CancellationToken cancellationToken = default)
            => _context.UserVocabReview.FirstOrDefaultAsync(x => x.UserId == userId && x.VocabId == vocabId, cancellationToken);

        public async Task AddUserPatternReviewAsync(UserPatternReview review, CancellationToken cancellationToken = default)
            => await _context.UserPatternReview.AddAsync(review, cancellationToken);

        public async Task AddUserVocabReviewAsync(UserVocabReview review, CancellationToken cancellationToken = default)
            => await _context.UserVocabReview.AddAsync(review, cancellationToken);

        public async Task AddPatternsAsync(IEnumerable<SentencePattern> patterns, CancellationToken cancellationToken = default)
            => await _context.SentencePatterns.AddRangeAsync(patterns, cancellationToken);

        public async Task AddVocabAsync(IEnumerable<VocabExpression> vocab, CancellationToken cancellationToken = default)
            => await _context.VocabExpressions.AddRangeAsync(vocab, cancellationToken);
    }
}
