using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.QuestionBank.Commands.UpdateQuestionBankCategory
{
    public record UpdateQuestionBankCategoryResult(
        Guid Id,
        CategoryType CategoryType,
        string Name,
        Guid? ParentId,
        string? Description);
}
