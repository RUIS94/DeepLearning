using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class AiCallLogRepository : IAiCallLogRepository
    {
        private readonly AppDbContext _context;

        public AiCallLogRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(AiCallLog log, CancellationToken cancellationToken = default)
            => await _context.AiCallLogs.AddAsync(log, cancellationToken);
    }
}
