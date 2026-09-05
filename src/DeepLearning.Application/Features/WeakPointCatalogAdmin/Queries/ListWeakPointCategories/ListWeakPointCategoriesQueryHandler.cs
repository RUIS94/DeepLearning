using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.WeakPointCatalogAdmin.Queries.ListWeakPointCategories
{
    public class ListWeakPointCategoriesQueryHandler
        : IRequestHandler<ListWeakPointCategoriesQuery, List<WeakPointCategoryResultItem>>
    {
        private readonly IWeakPointCategoryRepository _categoryRepository;

        public ListWeakPointCategoriesQueryHandler(IWeakPointCategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }

        public async Task<List<WeakPointCategoryResultItem>> Handle(ListWeakPointCategoriesQuery request, CancellationToken cancellationToken)
        {
            var rows = await _categoryRepository.ListAllAsync(cancellationToken);
            return rows
                .Select(x => new WeakPointCategoryResultItem(x.Id, x.Code, x.Name, x.Description, x.DisplayOrder))
                .ToList();
        }
    }
}
