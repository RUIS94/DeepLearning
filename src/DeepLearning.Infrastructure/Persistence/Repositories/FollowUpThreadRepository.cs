using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class FollowUpThreadRepository : IFollowUpThreadRepository
    {
        private readonly AppDbContext _context;

        public FollowUpThreadRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<FollowUpThread?> GetByIdWithMessagesAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.FollowUpThreads
                .Include(x => x.Messages.OrderBy(m => m.CreatedAt))
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public Task<List<FollowUpThread>> ListBySubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default)
            => _context.FollowUpThreads
                .Include(x => x.Messages.OrderBy(m => m.CreatedAt))
                .Where(x => x.SubmissionId == submissionId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

        public Task<bool> HasOpenThreadForSubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default)
            => _context.FollowUpThreads.AnyAsync(
                x => x.SubmissionId == submissionId && x.Status == FollowUpThreadStatus.open,
                cancellationToken);

        public async Task AddAsync(FollowUpThread thread, CancellationToken cancellationToken = default)
            => await _context.FollowUpThreads.AddAsync(thread, cancellationToken);

        public async Task AddMessageAsync(FollowUpMessage message, CancellationToken cancellationToken = default)
            => await _context.FollowUpMessages.AddAsync(message, cancellationToken);
    }
}
