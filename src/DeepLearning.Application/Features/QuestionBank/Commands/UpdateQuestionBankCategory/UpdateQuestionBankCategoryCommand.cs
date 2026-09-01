using MediatR;

namespace DeepLearning.Application.Features.QuestionBank.Commands.UpdateQuestionBankCategory
{
    public record UpdateQuestionBankCategoryCommand(
        Guid Id,
        string Name,
        Guid? ParentId,
        string? Description) : IRequest<UpdateQuestionBankCategoryResult>;
}
