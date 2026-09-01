using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class PromptTemplateRepository : IPromptTemplateRepository
    {
        private readonly AppDbContext _context;

        public PromptTemplateRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<PromptTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.PromptTemplates.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public Task<List<PromptTemplate>> ListAsync(
            Guid? examTypeId,
            SubjectCategory? subjectCategory,
            AiOperationType? templateType,
            bool? isActive,
            CancellationToken cancellationToken = default)
        {
            var query = _context.PromptTemplates.AsQueryable();

            if (isActive.HasValue)
            {
                query = query.Where(x => x.IsActive == isActive.Value);
            }

            if (examTypeId.HasValue)
            {
                query = query.Where(x => x.ExamTypeId == examTypeId.Value);
            }

            if (subjectCategory.HasValue)
            {
                query = query.Where(x => x.SubjectCategory == subjectCategory.Value);
            }

            if (templateType.HasValue)
            {
                query = query.Where(x => x.TemplateType == templateType.Value);
            }

            return query.OrderByDescending(x => x.Version).ToListAsync(cancellationToken);
        }

        public async Task AddAsync(PromptTemplate template, CancellationToken cancellationToken = default)
            => await _context.PromptTemplates.AddAsync(template, cancellationToken);

        public void Remove(PromptTemplate template)
            => _context.PromptTemplates.Remove(template);
    }
}
