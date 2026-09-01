using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.QuestionBank.Queries.ListQuestionBankCategories
{
    public class ListQuestionBankCategoriesQueryHandler : IRequestHandler<ListQuestionBankCategoriesQuery, List<ListQuestionBankCategoriesResultItem>>
    {
        private readonly IQuestionBankCategoryRepository _categoryRepository;

        public ListQuestionBankCategoriesQueryHandler(IQuestionBankCategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }

        public async Task<List<ListQuestionBankCategoriesResultItem>> Handle(ListQuestionBankCategoriesQuery request, CancellationToken cancellationToken)
        {
            var categories = await _categoryRepository.ListAsync(request.CategoryType, cancellationToken);

            return categories
                .Select(x => new ListQuestionBankCategoriesResultItem(x.Id, x.CategoryType, x.Name, x.ParentId, x.Description))
                .ToList();
        }
    }
}
