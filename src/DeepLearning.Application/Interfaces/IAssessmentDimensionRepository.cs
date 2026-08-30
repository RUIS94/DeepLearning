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

        /// <summary>
        /// Design doc §10.1 rubric versioning: the currently-open-ended version(s) of this
        /// dimension_key (EffectiveTo IS NULL) — i.e. whatever hasn't yet been superseded by a
        /// later revision. Used by CreateAssessmentDimensionCommandHandler to close these out
        /// (set EffectiveTo) when a new version is inserted, so ListByExamTypeAsync never has to
        /// choose between two simultaneously-"current" rows for the same dimension_key.
        /// </summary>
        Task<List<AssessmentDimension>> ListOpenEndedByKeyAsync(
            Guid examTypeId,
            string dimensionKey,
            CancellationToken cancellationToken = default);

        Task AddAsync(AssessmentDimension dimension, CancellationToken cancellationToken = default);
    }
}
