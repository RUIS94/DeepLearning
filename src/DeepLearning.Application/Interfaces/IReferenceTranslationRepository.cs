using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Interfaces
{
    public interface IReferenceTranslationRepository
    {
        Task<ReferenceTranslation?> GetByQuestionIdAsync(Guid questionId, CancellationToken cancellationToken = default);

        Task AddAsync(ReferenceTranslation referenceTranslation, CancellationToken cancellationToken = default);
    }
}
