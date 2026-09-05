using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class AiOperationProviderOverrideRepository : IAiOperationProviderOverrideRepository
    {
        private readonly AppDbContext _context;

        public AiOperationProviderOverrideRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<AiOperationProviderOverride?> GetByOperationTypeAsync(
            AiOperationType operationType, CancellationToken cancellationToken = default)
            => _context.AiOperationProviderOverrides
                .FirstOrDefaultAsync(x => x.OperationType == operationType, cancellationToken);

        public Task<List<AiOperationProviderOverride>> ListAsync(CancellationToken cancellationToken = default)
            => _context.AiOperationProviderOverrides.OrderBy(x => x.OperationType).ToListAsync(cancellationToken);

        public async Task AddAsync(AiOperationProviderOverride entity, CancellationToken cancellationToken = default)
            => await _context.AiOperationProviderOverrides.AddAsync(entity, cancellationToken);

        public void Remove(AiOperationProviderOverride entity)
            => _context.AiOperationProviderOverrides.Remove(entity);
    }
}
