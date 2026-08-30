using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class ReferenceTranslationRepository : IReferenceTranslationRepository
    {
        private readonly AppDbContext _context;

        public ReferenceTranslationRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<ReferenceTranslation?> GetByQuestionIdAsync(Guid questionId, CancellationToken cancellationToken = default)
            => _context.ReferenceTranslations.FirstOrDefaultAsync(x => x.QuestionId == questionId, cancellationToken);

        public async Task AddAsync(ReferenceTranslation referenceTranslation, CancellationToken cancellationToken = default)
            => await _context.ReferenceTranslations.AddAsync(referenceTranslation, cancellationToken);
    }
}
