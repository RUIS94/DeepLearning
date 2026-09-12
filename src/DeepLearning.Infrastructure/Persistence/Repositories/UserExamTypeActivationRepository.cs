using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class UserExamTypeActivationRepository : IUserExamTypeActivationRepository
    {
        private readonly AppDbContext _context;

        public UserExamTypeActivationRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<UserExamTypeActivation?> GetAsync(Guid userId, Guid examTypeId, CancellationToken cancellationToken = default)
            => _context.UserExamTypeActivations.AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId && x.ExamTypeId == examTypeId, cancellationToken);

        public Task<List<UserExamTypeActivation>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default)
            => _context.UserExamTypeActivations.AsNoTracking()
                .Where(x => x.UserId == userId)
                .ToListAsync(cancellationToken);

        public async Task SetActiveAsync(Guid userId, Guid examTypeId, bool isActive, CancellationToken cancellationToken = default)
        {
            var existing = await _context.UserExamTypeActivations
                .FirstOrDefaultAsync(x => x.UserId == userId && x.ExamTypeId == examTypeId, cancellationToken);

            if (existing is null)
            {
                await _context.UserExamTypeActivations.AddAsync(new UserExamTypeActivation
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ExamTypeId = examTypeId,
                    IsActive = isActive,
                    ActivatedAt = DateTimeOffset.UtcNow,
                }, cancellationToken);
                return;
            }

            existing.IsActive = isActive;
            existing.ActivatedAt = DateTimeOffset.UtcNow;
        }
    }
}
