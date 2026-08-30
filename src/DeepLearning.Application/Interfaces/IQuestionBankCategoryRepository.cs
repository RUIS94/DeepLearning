using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Interfaces
{
    public interface IQuestionBankCategoryRepository
    {
        Task<QuestionBankCategory?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<List<QuestionBankCategory>> ListAsync(CategoryType? categoryType, CancellationToken cancellationToken = default);

        Task AddAsync(QuestionBankCategory category, CancellationToken cancellationToken = default);
    }
}
