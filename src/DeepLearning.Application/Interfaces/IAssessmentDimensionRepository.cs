using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Interfaces
{
    public interface IAssessmentDimensionRepository
    {
        Task<AssessmentDimension?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<List<AssessmentDimension>> ListByExamTypeAsync(
            Guid examTypeId,
            TaskType? applicableTaskType,
            CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(
            Guid examTypeId,
            string dimensionKey,
            string rubricVersion,
            CancellationToken cancellationToken = default);

        Task AddAsync(AssessmentDimension dimension, CancellationToken cancellationToken = default);
    }
}
