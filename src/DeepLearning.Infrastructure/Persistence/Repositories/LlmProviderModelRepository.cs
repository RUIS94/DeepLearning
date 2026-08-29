using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class LlmProviderModelRepository : ILlmProviderModelRepository
    {
        private readonly AppDbContext _context;

        public LlmProviderModelRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(LlmProviderModel model, CancellationToken cancellationToken = default)
            => await _context.LlmProviderModels.AddAsync(model, cancellationToken);

        public Task<LlmProviderModel?> GetByProviderKeyAndModelAsync(string providerKey, string model, CancellationToken cancellationToken = default)
            => _context.LlmProviderModels.FirstOrDefaultAsync(x => x.ProviderKey == providerKey && x.Model == model, cancellationToken);

        public Task<LlmProviderModel?> GetCurrentAsync(string providerKey, CancellationToken cancellationToken = default)
            => _context.LlmProviderModels.FirstOrDefaultAsync(x => x.ProviderKey == providerKey && x.IsCurrent, cancellationToken);

        public Task<List<LlmProviderModel>> ListByProviderKeyAsync(string providerKey, CancellationToken cancellationToken = default)
            => _context.LlmProviderModels
                .Where(x => x.ProviderKey == providerKey)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

        public Task<List<LlmProviderModel>> ListCurrentAsync(CancellationToken cancellationToken = default)
            => _context.LlmProviderModels.Where(x => x.IsCurrent).ToListAsync(cancellationToken);
    }
}
