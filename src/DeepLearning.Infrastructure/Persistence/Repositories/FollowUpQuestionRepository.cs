using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class FollowUpQuestionRepository : IFollowUpQuestionRepository
    {
        private readonly AppDbContext _context;

        public FollowUpQuestionRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<FollowUpQuestion?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.FollowUpQuestions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public async Task AddAsync(FollowUpQuestion followUpQuestion, CancellationToken cancellationToken = default)
            => await _context.FollowUpQuestions.AddAsync(followUpQuestion, cancellationToken);
    }
}
