using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Queries.GetErrorTaxonomiesByExamType
{
    public class GetErrorTaxonomiesByExamTypeQueryHandler
        : IRequestHandler<GetErrorTaxonomiesByExamTypeQuery, List<ErrorTaxonomyResultItem>>
    {
        private readonly IErrorTaxonomyRepository _taxonomyRepository;

        public GetErrorTaxonomiesByExamTypeQueryHandler(IErrorTaxonomyRepository taxonomyRepository)
        {
            _taxonomyRepository = taxonomyRepository;
        }

        public async Task<List<ErrorTaxonomyResultItem>> Handle(
            GetErrorTaxonomiesByExamTypeQuery request,
            CancellationToken cancellationToken)
        {
            var taxonomies = await _taxonomyRepository.ListByExamTypeAsync(request.ExamTypeId, cancellationToken);

            return taxonomies.Select(x => new ErrorTaxonomyResultItem(
                x.Id, x.ExamTypeId, x.CategoryKey, x.CategoryName, x.Description, x.ExampleCases)).ToList();
        }
    }
}
