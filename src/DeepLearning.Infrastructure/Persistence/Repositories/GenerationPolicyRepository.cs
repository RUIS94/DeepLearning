using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class GenerationPolicyRepository : IGenerationPolicyRepository
    {
        private readonly AppDbContext _context;

        public GenerationPolicyRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<GenerationPolicy?> GetByKeyAsync(Guid examTypeId, string policyKey, CancellationToken cancellationToken = default)
            => _context.GenerationPolicies.FirstOrDefaultAsync(
                x => x.ExamTypeId == examTypeId && x.PolicyKey == policyKey, cancellationToken);
    }
}
