using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.WeakPointCatalogAdmin.Queries.ListWeakPointCatalog
{
    public class ListWeakPointCatalogQueryHandler
        : IRequestHandler<ListWeakPointCatalogQuery, List<WeakPointCatalogResultItem>>
    {
        private readonly IWeakPointCatalogRepository _catalogRepository;

        public ListWeakPointCatalogQueryHandler(IWeakPointCatalogRepository catalogRepository)
        {
            _catalogRepository = catalogRepository;
        }

        public async Task<List<WeakPointCatalogResultItem>> Handle(ListWeakPointCatalogQuery request, CancellationToken cancellationToken)
        {
            var rows = await _catalogRepository.ListAllByExamTypeAsync(request.ExamTypeId, cancellationToken);

            return rows
                .Where(x => request.Status is null || x.Status == request.Status)
                .Select(x => new WeakPointCatalogResultItem(
                    x.Id,
                    x.ExamTypeId,
                    x.Code,
                    x.Name,
                    x.Description,
                    x.DefaultDimensionKey,
                    x.DefaultErrorCategory,
                    x.Status,
                    x.Origin,
                    x.CreatedAt))
                .ToList();
        }
    }
}
