using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.LlmProviders.Commands.AddLlmProviderModel
{
    public class AddLlmProviderModelCommandHandler : IRequestHandler<AddLlmProviderModelCommand, AddLlmProviderModelResult>
    {
        private readonly ILlmProviderSettingsRepository _providerRepository;
        private readonly ILlmProviderModelRepository _modelRepository;
        private readonly IUnitOfWork _unitOfWork;

        public AddLlmProviderModelCommandHandler(
            ILlmProviderSettingsRepository providerRepository,
            ILlmProviderModelRepository modelRepository,
            IUnitOfWork unitOfWork)
        {
            _providerRepository = providerRepository;
            _modelRepository = modelRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<AddLlmProviderModelResult> Handle(AddLlmProviderModelCommand request, CancellationToken cancellationToken)
        {
            _ = await _providerRepository.GetByProviderKeyAsync(request.ProviderKey, cancellationToken)
                ?? throw new NotFoundException(nameof(Domain.Entities.LlmProviderSettings), request.ProviderKey);

            var existing = await _modelRepository.GetByProviderKeyAndModelAsync(request.ProviderKey, request.Model, cancellationToken);
            if (existing is not null)
            {
                throw new ConflictException($"Model '{request.Model}' is already cataloged for provider '{request.ProviderKey}'.");
            }

            var model = new LlmProviderModel
            {
                Id = Guid.NewGuid(),
                ProviderKey = request.ProviderKey,
                Model = request.Model,
                Label = request.Label,
                IsCurrent = false,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await _modelRepository.AddAsync(model, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new AddLlmProviderModelResult(model.ProviderKey, model.Model, model.Label, model.IsCurrent, model.CreatedAt);
        }
    }
}
