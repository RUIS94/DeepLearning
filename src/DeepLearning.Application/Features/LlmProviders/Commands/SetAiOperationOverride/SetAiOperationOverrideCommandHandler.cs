using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.LlmProviders.Commands.SetAiOperationOverride
{
    public class SetAiOperationOverrideCommandHandler
        : IRequestHandler<SetAiOperationOverrideCommand, SetAiOperationOverrideResult>
    {
        private readonly IAiOperationProviderOverrideRepository _overrideRepository;
        private readonly ILlmProviderSettingsRepository _settingsRepository;
        private readonly ILlmProviderModelRepository _modelRepository;
        private readonly IUnitOfWork _unitOfWork;

        public SetAiOperationOverrideCommandHandler(
            IAiOperationProviderOverrideRepository overrideRepository,
            ILlmProviderSettingsRepository settingsRepository,
            ILlmProviderModelRepository modelRepository,
            IUnitOfWork unitOfWork)
        {
            _overrideRepository = overrideRepository;
            _settingsRepository = settingsRepository;
            _modelRepository = modelRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<SetAiOperationOverrideResult> Handle(SetAiOperationOverrideCommand request, CancellationToken cancellationToken)
        {
            // Fail here rather than leaving a dangling override that LlmClientResolver would
            // silently fall back past later — a typo'd provider key should be rejected at the
            // moment it's set, not discovered the next time this operation runs.
            _ = await _settingsRepository.GetByProviderKeyAsync(request.ProviderKey, cancellationToken)
                ?? throw new NotFoundException(nameof(Domain.Entities.LlmProviderSettings), request.ProviderKey);

            if (request.Model is not null)
            {
                _ = await _modelRepository.GetByProviderKeyAndModelAsync(request.ProviderKey, request.Model, cancellationToken)
                    ?? throw new NotFoundException(nameof(Domain.Entities.LlmProviderModel), $"{request.ProviderKey}/{request.Model}");
            }

            var existing = await _overrideRepository.GetByOperationTypeAsync(request.OperationType, cancellationToken);
            var now = DateTimeOffset.UtcNow;

            if (existing is not null)
            {
                existing.ProviderKey = request.ProviderKey;
                existing.Model = request.Model;
                existing.ThinkingEnabled = request.ThinkingEnabled;
                existing.Effort = request.Effort;
                existing.UpdatedAt = now;
            }
            else
            {
                existing = new AiOperationProviderOverride
                {
                    OperationType = request.OperationType,
                    ProviderKey = request.ProviderKey,
                    Model = request.Model,
                    ThinkingEnabled = request.ThinkingEnabled,
                    Effort = request.Effort,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                await _overrideRepository.AddAsync(existing, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new SetAiOperationOverrideResult(
                existing.OperationType, existing.ProviderKey, existing.Model, existing.ThinkingEnabled, existing.Effort, existing.UpdatedAt);
        }
    }
}
