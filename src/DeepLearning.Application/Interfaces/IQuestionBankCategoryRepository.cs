using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Interfaces
{
    public interface IQuestionBankCategoryRepository
    {
        Task<QuestionBankCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <param name="examTypeId">
        /// When set, restricts to that exam type's categories plus global ones
        /// (<c>exam_type_id IS NULL</c>). Null = every category.
        /// </param>
        Task<List<QuestionBankCategory>> ListAsync(
            CategoryType? categoryType, Guid? examTypeId = null, CancellationToken cancellationToken = default);

        Task AddAsync(QuestionBankCategory category, CancellationToken cancellationToken = default);

        void Remove(QuestionBankCategory category);

        /// <summary>True if any other category has this one as its ParentId.</summary>
        Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>True if any question is tagged with this category (question_category_map).</summary>
        Task<bool> IsReferencedByQuestionsAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
