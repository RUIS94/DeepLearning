using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Queries.ListMyExamTypeActivations
{
    public class ListMyExamTypeActivationsQueryHandler
        : IRequestHandler<ListMyExamTypeActivationsQuery, List<ExamTypeActivationResultItem>>
    {
        private readonly IExamTypeRepository _examTypeRepository;
        private readonly IUserExamTypeActivationRepository _activationRepository;

        public ListMyExamTypeActivationsQueryHandler(
            IExamTypeRepository examTypeRepository, IUserExamTypeActivationRepository activationRepository)
        {
            _examTypeRepository = examTypeRepository;
            _activationRepository = activationRepository;
        }

        public async Task<List<ExamTypeActivationResultItem>> Handle(ListMyExamTypeActivationsQuery request, CancellationToken cancellationToken)
        {
            var examTypes = await _examTypeRepository.ListAsync(isActive: true, cancellationToken);
            var activations = await _activationRepository.ListByUserAsync(request.UserId, cancellationToken);
            var activatedIds = activations.Where(a => a.IsActive).Select(a => a.ExamTypeId).ToHashSet();

            return examTypes
                .Select(e => new ExamTypeActivationResultItem(e.Id, e.Code, e.Name, activatedIds.Contains(e.Id)))
                .ToList();
        }
    }
}
