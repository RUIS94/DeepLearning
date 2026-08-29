using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class LlmProviderSettingsRepository : ILlmProviderSettingsRepository
    {
        private readonly AppDbContext _context;

        public LlmProviderSettingsRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<LlmProviderSettings?> GetActiveAsync(CancellationToken cancellationToken = default)
            => _context.LlmProviderSettings.FirstOrDefaultAsync(x => x.IsActive, cancellationToken);

        public Task<LlmProviderSettings?> GetByProviderKeyAsync(string providerKey, CancellationToken cancellationToken = default)
            => _context.LlmProviderSettings.FirstOrDefaultAsync(x => x.ProviderKey == providerKey, cancellationToken);

        public Task<List<LlmProviderSettings>> ListAsync(CancellationToken cancellationToken = default)
            => _context.LlmProviderSettings.OrderBy(x => x.ProviderKey).ToListAsync(cancellationToken);
    }
}
