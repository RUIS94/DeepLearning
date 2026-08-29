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
                // Two separate saves, deliberately: the partial unique index on IsActive
                // (WHERE is_active = true) is checked per-statement, not deferred, so setting
                // the new row to true before the old one is set to false would transiently
                // violate it within the same transaction.
                var all = await _repository.ListAsync(cancellationToken);
                foreach (var other in all.Where(x => x.IsActive && x.Id != target.Id))
                {
                    other.IsActive = false;
                    other.UpdatedAt = DateTimeOffset.UtcNow;
                }
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                target.IsActive = true;
                target.UpdatedAt = DateTimeOffset.UtcNow;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return new ActivateLlmProviderResult(target.ProviderKey, target.IsActive);
        }
    }
}
