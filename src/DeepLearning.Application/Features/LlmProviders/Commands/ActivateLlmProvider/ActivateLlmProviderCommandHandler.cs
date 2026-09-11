using DeepLearning.Application.Common;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.LlmProviders.Commands.ActivateLlmProvider
{
    public class ActivateLlmProviderCommandHandler : IRequestHandler<ActivateLlmProviderCommand, ActivateLlmProviderResult>
    {
        private readonly ILlmProviderSettingsRepository _repository;
        private readonly IUnitOfWork _unitOfWork;

        public ActivateLlmProviderCommandHandler(ILlmProviderSettingsRepository repository, IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
        }

        public async Task<ActivateLlmProviderResult> Handle(ActivateLlmProviderCommand request, CancellationToken cancellationToken)
        {
            var target = await _repository.GetByProviderKeyAsync(request.ProviderKey, cancellationToken)
                ?? throw new NotFoundException(nameof(Domain.Entities.LlmProviderSettings), request.ProviderKey);

            if (!target.IsActive)
            {
                var all = await _repository.ListAsync(cancellationToken);
                await ExclusiveFlag.SetAsync(
                    all,
                    target,
                    get: x => x.IsActive,
                    set: (x, v) =>
                    {
                        x.IsActive = v;
                        x.UpdatedAt = DateTimeOffset.UtcNow;
                    },
                    _unitOfWork,
                    cancellationToken);
            }

            return new ActivateLlmProviderResult(target.ProviderKey, target.IsActive);
        }
    }
}
