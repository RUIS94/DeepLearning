using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.QuestionBank.Queries.ListQuestionBankCategories
{
    public record ListQuestionBankCategoriesQuery(
        CategoryType? CategoryType,
        Guid? ExamTypeId = null) : IRequest<List<ListQuestionBankCategoriesResultItem>>;
}
