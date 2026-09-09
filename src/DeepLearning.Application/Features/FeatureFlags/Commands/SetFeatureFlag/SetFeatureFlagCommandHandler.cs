using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.FeatureFlags.Commands.SetFeatureFlag
{
    public class SetFeatureFlagCommandHandler
        : IRequestHandler<SetFeatureFlagCommand, SetFeatureFlagResult>
    {
        private readonly IFeatureFlagRepository _repository;
        private readonly IFeatureFlagService _service;
        private readonly IUnitOfWork _unitOfWork;

        public SetFeatureFlagCommandHandler(
            IFeatureFlagRepository repository,
            IFeatureFlagService service,
            IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _service = service;
            _unitOfWork = unitOfWork;
        }

        public async Task<SetFeatureFlagResult> Handle(
            SetFeatureFlagCommand request, CancellationToken cancellationToken)
        {
            await _repository.SetEnabledAsync(request.Key, request.Enabled, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _service.Invalidate(request.Key);

            return new SetFeatureFlagResult(request.Key, request.Enabled);
        }
    }
}
