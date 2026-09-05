using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.LlmProviders.Commands.ClearAiOperationOverride
{
    public class ClearAiOperationOverrideCommandHandler : IRequestHandler<ClearAiOperationOverrideCommand>
    {
        private readonly IAiOperationProviderOverrideRepository _overrideRepository;
        private readonly IUnitOfWork _unitOfWork;

        public ClearAiOperationOverrideCommandHandler(IAiOperationProviderOverrideRepository overrideRepository, IUnitOfWork unitOfWork)
        {
            _overrideRepository = overrideRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(ClearAiOperationOverrideCommand request, CancellationToken cancellationToken)
        {
            var existing = await _overrideRepository.GetByOperationTypeAsync(request.OperationType, cancellationToken);
            if (existing is null)
            {
                return;
            }

            _overrideRepository.Remove(existing);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
