using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.QuestionBank.Commands.CreateQuestionBankCategory
{
    public record CreateQuestionBankCategoryCommand(
        CategoryType CategoryType,
        string Name,
        Guid? ParentId,
        string? Description) : IRequest<CreateQuestionBankCategoryResult>;
}
