using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Interfaces
{
    public interface IPromptTemplateRepository
    {
        Task<PromptTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<List<PromptTemplate>> ListAsync(
            Guid? examTypeId,
            SubjectCategory? subjectCategory,
            AiOperationType? templateType,
            bool? isActive,
            CancellationToken cancellationToken = default);

        Task AddAsync(PromptTemplate template, CancellationToken cancellationToken = default);

        void Remove(PromptTemplate template);
    }
}
