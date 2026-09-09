using MediatR;

namespace DeepLearning.Application.Features.QuestionBank.Commands.UpdateQuestionBankCategory
{
    public record UpdateQuestionBankCategoryCommand(
        Guid Id,
        string Name,
        Guid? ParentId,
        string? Description,
        Guid? ExamTypeId = null) : IRequest<UpdateQuestionBankCategoryResult>;
}
