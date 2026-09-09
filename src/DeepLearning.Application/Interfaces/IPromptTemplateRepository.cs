using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Interfaces
{
    public interface IPromptTemplateRepository
    {
        Task<PromptTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <param name="includeGlobalScope">
        /// Only meaningful when <paramref name="examTypeId"/> is set. <c>false</c> (the default,
        /// and what <c>ExamConfigLoader</c> relies on) matches that exam type's rows exactly.
        /// <c>true</c> also returns rows with a NULL <c>exam_type_id</c> (the shared_methodology
        /// layer) — for the admin surface, which shows an exam type's own rows plus the shared
        /// ones it inherits.
        /// </param>
        Task<List<PromptTemplate>> ListAsync(
            Guid? examTypeId,
            SubjectCategory? subjectCategory,
            AiOperationType? templateType,
            bool? isActive,
            bool includeGlobalScope = false,
            CancellationToken cancellationToken = default);

        Task AddAsync(PromptTemplate template, CancellationToken cancellationToken = default);

        void Remove(PromptTemplate template);
    }
}
