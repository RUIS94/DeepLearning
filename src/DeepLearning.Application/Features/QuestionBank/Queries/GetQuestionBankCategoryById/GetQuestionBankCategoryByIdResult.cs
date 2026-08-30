using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.QuestionBank.Queries.GetQuestionBankCategoryById
{
    public record GetQuestionBankCategoryByIdResult(
        Guid Id,
        CategoryType CategoryType,
        string Name,
        Guid? ParentId,
        string? Description,
        DateTimeOffset CreatedAt);
}
