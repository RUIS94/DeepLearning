using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class GradingSummaryRepository : IGradingSummaryRepository
    {
        private readonly AppDbContext _context;

        public GradingSummaryRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<GradingSummary?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken cancellationToken = default)
            => _context.GradingSummaries.FirstOrDefaultAsync(x => x.SubmissionId == submissionId, cancellationToken);

        public async Task AddAsync(GradingSummary summary, CancellationToken cancellationToken = default)
            => await _context.GradingSummaries.AddAsync(summary, cancellationToken);
    }
}
