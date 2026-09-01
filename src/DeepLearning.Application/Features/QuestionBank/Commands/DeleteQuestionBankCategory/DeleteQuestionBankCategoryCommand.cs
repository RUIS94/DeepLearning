using MediatR;

namespace DeepLearning.Application.Features.QuestionBank.Commands.DeleteQuestionBankCategory
{
    public record DeleteQuestionBankCategoryCommand(Guid Id) : IRequest;
}
