using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DeepLearning.Infrastructure.Persistence.Repositories
{
    public class AssessmentDimensionRepository : IAssessmentDimensionRepository
    {
        private readonly AppDbContext _context;

        public AssessmentDimensionRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<AssessmentDimension?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => _context.AssessmentDimensions.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        public Task<List<AssessmentDimension>> ListByExamTypeAsync(
            Guid examTypeId,
            TaskType? applicableTaskType,
            CancellationToken cancellationToken = default)
        {
            var query = _context.AssessmentDimensions.Where(x => x.ExamTypeId == examTypeId);

            if (applicableTaskType.HasValue)
            {
                query = query.Where(x => x.ApplicableTaskType == null || x.ApplicableTaskType == applicableTaskType.Value);
            }

            return query.OrderBy(x => x.DimensionKey).ToListAsync(cancellationToken);
        }

        public Task<bool> ExistsAsync(
            Guid examTypeId,
            string dimensionKey,
            string rubricVersion,
            CancellationToken cancellationToken = default)
            => _context.AssessmentDimensions.AnyAsync(
                x => x.ExamTypeId == examTypeId && x.DimensionKey == dimensionKey && x.RubricVersion == rubricVersion,
                cancellationToken);

        public async Task AddAsync(AssessmentDimension dimension, CancellationToken cancellationToken = default)
            => await _context.AssessmentDimensions.AddAsync(dimension, cancellationToken);
    }
}
