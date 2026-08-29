using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.LlmProviders.Commands.SelectLlmProviderModel
{
    public class SelectLlmProviderModelCommandHandler : IRequestHandler<SelectLlmProviderModelCommand, SelectLlmProviderModelResult>
    {
        private readonly ILlmProviderModelRepository _modelRepository;
        private readonly IUnitOfWork _unitOfWork;

        public SelectLlmProviderModelCommandHandler(ILlmProviderModelRepository modelRepository, IUnitOfWork unitOfWork)
        {
            _modelRepository = modelRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<SelectLlmProviderModelResult> Handle(SelectLlmProviderModelCommand request, CancellationToken cancellationToken)
        {
            var target = await _modelRepository.GetByProviderKeyAndModelAsync(request.ProviderKey, request.Model, cancellationToken)
                ?? throw new NotFoundException(nameof(Domain.Entities.LlmProviderModel), $"{request.ProviderKey}/{request.Model}");

            if (!target.IsCurrent)
            {
                // Two separate saves, deliberately: the partial unique index on ProviderKey
                // (WHERE is_current = true) is checked per-statement, not deferred, so setting
                // the new row to current before the old one is un-set would transiently
                // violate it within the same transaction. Same pattern as ActivateLlmProvider.
                var siblings = await _modelRepository.ListByProviderKeyAsync(request.ProviderKey, cancellationToken);
                foreach (var other in siblings.Where(x => x.IsCurrent && x.Id != target.Id))
                {
                    other.IsCurrent = false;
                }
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                target.IsCurrent = true;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return new SelectLlmProviderModelResult(target.ProviderKey, target.Model, target.IsCurrent);
        }
    }
}
