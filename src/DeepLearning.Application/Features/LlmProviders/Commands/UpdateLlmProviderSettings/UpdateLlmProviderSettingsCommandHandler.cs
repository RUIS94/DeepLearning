using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.LlmProviders.Commands.UpdateLlmProviderSettings
{
    public class UpdateLlmProviderSettingsCommandHandler
        : IRequestHandler<UpdateLlmProviderSettingsCommand, UpdateLlmProviderSettingsResult>
    {
        private readonly ILlmProviderSettingsRepository _repository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateLlmProviderSettingsCommandHandler(ILlmProviderSettingsRepository repository, IUnitOfWork unitOfWork)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
        }

        public async Task<UpdateLlmProviderSettingsResult> Handle(
            UpdateLlmProviderSettingsCommand request,
            CancellationToken cancellationToken)
        {
            var settings = await _repository.GetByProviderKeyAsync(request.ProviderKey, cancellationToken)
                ?? throw new NotFoundException(nameof(Domain.Entities.LlmProviderSettings), request.ProviderKey);

            if (request.ThinkingEnabled is not null)
            {
                settings.ThinkingEnabled = request.ThinkingEnabled.Value;
            }

            if (request.Effort is not null)
            {
                settings.Effort = request.Effort;
            }

            if (request.ExtraSettingsJson is not null)
            {
                settings.ExtraSettings = request.ExtraSettingsJson;
            }

            settings.UpdatedAt = DateTimeOffset.UtcNow;

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new UpdateLlmProviderSettingsResult(
                settings.ProviderKey,
                settings.IsActive,
                settings.ThinkingEnabled,
                settings.Effort,
                settings.ExtraSettings,
                settings.UpdatedAt);
        }
    }
}
