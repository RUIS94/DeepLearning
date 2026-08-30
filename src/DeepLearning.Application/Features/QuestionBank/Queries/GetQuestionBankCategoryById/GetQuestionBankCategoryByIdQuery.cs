using MediatR;

namespace DeepLearning.Application.Features.QuestionBank.Queries.GetQuestionBankCategoryById
{
    public record GetQuestionBankCategoryByIdQuery(Guid Id) : IRequest<GetQuestionBankCategoryByIdResult>;
}
