using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.QuestionBank.Queries.ListQuestionBankCategories
{
    public record ListQuestionBankCategoriesQuery(CategoryType? CategoryType) : IRequest<List<ListQuestionBankCategoriesResultItem>>;
}
