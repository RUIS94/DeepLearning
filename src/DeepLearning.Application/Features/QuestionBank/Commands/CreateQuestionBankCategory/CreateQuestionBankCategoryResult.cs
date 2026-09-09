using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.QuestionBank.Commands.CreateQuestionBankCategory
{
    public record CreateQuestionBankCategoryResult(
        Guid Id,
        CategoryType CategoryType,
        string Name,
        Guid? ParentId,
        Guid? ExamTypeId,
        DateTimeOffset CreatedAt);
}
