using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class QuestionBankCategoryRepository : IQuestionBankCategoryRepository
    {
        private readonly AppDbContext _context;

        public QuestionBankCategoryRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<QuestionBankCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.QuestionBankCategories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public Task<List<QuestionBankCategory>> ListAsync(CategoryType? categoryType, CancellationToken cancellationToken = default)
        {
            var query = _context.QuestionBankCategories.AsQueryable();

            if (categoryType.HasValue)
            {
                query = query.Where(x => x.CategoryType == categoryType.Value);
            }

            return query.OrderBy(x => x.Name).ToListAsync(cancellationToken);
        }

        public async Task AddAsync(QuestionBankCategory category, CancellationToken cancellationToken = default)
            => await _context.QuestionBankCategories.AddAsync(category, cancellationToken);

        public void Remove(QuestionBankCategory category)
            => _context.QuestionBankCategories.Remove(category);

        public Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.QuestionBankCategories.AnyAsync(x => x.ParentId == id, cancellationToken);

        public Task<bool> IsReferencedByQuestionsAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.QuestionCategoryMap.AnyAsync(x => x.CategoryId == id, cancellationToken);
    }
}
