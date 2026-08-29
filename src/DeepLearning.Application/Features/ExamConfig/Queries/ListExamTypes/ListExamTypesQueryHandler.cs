using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Queries.ListExamTypes
{
    public class ListExamTypesQueryHandler : IRequestHandler<ListExamTypesQuery, List<ListExamTypesResultItem>>
    {
        private readonly IExamTypeRepository _examTypeRepository;

        public ListExamTypesQueryHandler(IExamTypeRepository examTypeRepository)
        {
            _examTypeRepository = examTypeRepository;
        }

        public async Task<List<ListExamTypesResultItem>> Handle(ListExamTypesQuery request, CancellationToken cancellationToken)
        {
            var examTypes = await _examTypeRepository.ListAsync(request.IsActive, cancellationToken);

            return examTypes
                .Select(x => new ListExamTypesResultItem(x.Id, x.Code, x.Name, x.SubjectCategory, x.IsActive))
                .ToList();
        }
    }
}
