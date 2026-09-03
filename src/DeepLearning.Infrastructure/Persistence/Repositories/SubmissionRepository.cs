using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class SubmissionRepository : ISubmissionRepository
    {
        private readonly AppDbContext _context;

        public SubmissionRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<Submission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.Submissions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public Task<List<Submission>> ListByUserAsync(Guid userId, Guid? questionId, CancellationToken cancellationToken = default)
            => _context.Submissions
                .Where(x => x.UserId == userId && (questionId == null || x.QuestionId == questionId))
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

        public Task<List<DateTimeOffset>> ListRecentGradedCreatedAtAsync(Guid userId, int count, CancellationToken cancellationToken = default)
            => _context.Submissions
                .Where(x => x.UserId == userId && x.Status == SubmissionStatus.graded)
                .OrderByDescending(x => x.CreatedAt)
                .Take(count)
                .Select(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

        public Task<List<GradingResult>> GetGradingResultsAsync(Guid submissionId, CancellationToken cancellationToken = default)
            => _context.GradingResults
                .Where(x => x.SubmissionId == submissionId)
                .Include(x => x.Dimension)
                .ToListAsync(cancellationToken);

        public Task<List<ErrorListItem>> GetErrorListAsync(Guid submissionId, CancellationToken cancellationToken = default)
            => _context.ErrorList
                .Where(x => x.SubmissionId == submissionId)
                .Include(x => x.ErrorTaxonomy)
                .Include(x => x.Dimension)
                .ToListAsync(cancellationToken);

        public async Task AddAsync(Submission submission, CancellationToken cancellationToken = default)
            => await _context.Submissions.AddAsync(submission, cancellationToken);

        public async Task AddGradingResultsAsync(IEnumerable<GradingResult> results, CancellationToken cancellationToken = default)
            => await _context.GradingResults.AddRangeAsync(results, cancellationToken);

        public async Task AddErrorListItemsAsync(IEnumerable<ErrorListItem> items, CancellationToken cancellationToken = default)
            => await _context.ErrorList.AddRangeAsync(items, cancellationToken);
    }
}
