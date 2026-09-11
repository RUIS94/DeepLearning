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

            // Partial-update convention (matches UpdateWeakPointCatalogEntryCommandHandler): null
            // = leave the field unchanged, "" = clear it to null (Effort/ExtraSettings are both
            // nullable columns; Effort null means "let the provider default", ExtraSettings null
            // means no JSON passthrough). Previously "" was stored literally instead of clearing
            // to null, so the frontend's "reset effort to Default" (which sent effort: null,
            // meaning "don't touch" under this convention) could never actually clear a
            // previously-set Effort back to null — fixed together with the frontend sending ""
            // instead of null for "reset" (代码复用扫描_07_优化计划.md §3.6/N4 follow-up).
            if (request.Effort is not null)
            {
                settings.Effort = request.Effort.Length == 0 ? null : request.Effort;
            }

            if (request.ExtraSettingsJson is not null)
            {
                settings.ExtraSettings = request.ExtraSettingsJson.Length == 0 ? null : request.ExtraSettingsJson;
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
