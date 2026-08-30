using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.QuestionBank.Queries.ListQuestionBankCategories
{
    public record ListQuestionBankCategoriesResultItem(
        Guid Id,
        CategoryType CategoryType,
        string Name,
        Guid? ParentId);
}
