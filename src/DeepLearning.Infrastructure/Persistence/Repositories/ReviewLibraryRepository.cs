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
