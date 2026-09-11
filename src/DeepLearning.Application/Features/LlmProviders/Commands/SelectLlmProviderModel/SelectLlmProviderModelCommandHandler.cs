using DeepLearning.Application.Common;
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
                var siblings = await _modelRepository.ListByProviderKeyAsync(request.ProviderKey, cancellationToken);
                await ExclusiveFlag.SetAsync(
                    siblings,
                    target,
                    get: x => x.IsCurrent,
                    set: (x, v) => x.IsCurrent = v,
                    _unitOfWork,
                    cancellationToken);
            }

            return new SelectLlmProviderModelResult(target.ProviderKey, target.Model, target.IsCurrent);
        }
    }
}
