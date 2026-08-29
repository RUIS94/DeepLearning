using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class ExamTypeRepository : IExamTypeRepository
    {
        private readonly AppDbContext _context;

        public ExamTypeRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<ExamType?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.ExamTypes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public Task<ExamType?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
            => _context.ExamTypes.FirstOrDefaultAsync(x => x.Code == code, cancellationToken);

        public Task<List<ExamType>> ListAsync(bool? isActive, CancellationToken cancellationToken = default)
        {
            var query = _context.ExamTypes.AsQueryable();

            if (isActive.HasValue)
            {
                query = query.Where(x => x.IsActive == isActive.Value);
            }

            return query.OrderBy(x => x.Code).ToListAsync(cancellationToken);
        }

        public async Task AddAsync(ExamType examType, CancellationToken cancellationToken = default)
            => await _context.ExamTypes.AddAsync(examType, cancellationToken);
    }
}
