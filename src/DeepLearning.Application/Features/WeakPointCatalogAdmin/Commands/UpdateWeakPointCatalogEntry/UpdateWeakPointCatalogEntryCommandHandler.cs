using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.WeakPointCatalogAdmin.Commands.UpdateWeakPointCatalogEntry
{
    public class UpdateWeakPointCatalogEntryCommandHandler
        : IRequestHandler<UpdateWeakPointCatalogEntryCommand, UpdateWeakPointCatalogEntryResult>
    {
        private readonly IWeakPointCatalogRepository _catalogRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateWeakPointCatalogEntryCommandHandler(
            IWeakPointCatalogRepository catalogRepository, IUnitOfWork unitOfWork)
        {
            _catalogRepository = catalogRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<UpdateWeakPointCatalogEntryResult> Handle(UpdateWeakPointCatalogEntryCommand request, CancellationToken cancellationToken)
        {
            var entry = await _catalogRepository.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(WeakPointCatalog), request.Id);

            // Partial-update convention (consistent across this handler and
            // UpdateLlmProviderSettingsCommandHandler): null = leave the field unchanged, any
            // other value (including "") = set it. For a nullable column that means "" clears it
            // to null; Name/Description aren't nullable, so "" just sets them to empty — both are
            // "the field's cleared state" for their respective column. Previously Name/Description
            // used an IsNullOrWhiteSpace guard instead (silently ignoring a blank submission rather
            // than treating it as "clear"), inconsistent with DefaultDimensionKey/DefaultErrorCategory
            // below — confirmed safe to unify because the admin form's zod schema already requires
            // non-blank name/description client-side, so this path never actually receives "" today
            // (代码复用扫描_07_优化计划.md §3.6/N4).
            if (request.Name is not null)
            {
                entry.Name = request.Name;
            }

            if (request.Description is not null)
            {
                entry.Description = request.Description;
            }

            if (request.DefaultDimensionKey is not null)
            {
                entry.DefaultDimensionKey = request.DefaultDimensionKey.Length == 0 ? null : request.DefaultDimensionKey;
            }

            if (request.DefaultErrorCategory is not null)
            {
                entry.DefaultErrorCategory = request.DefaultErrorCategory.Length == 0 ? null : request.DefaultErrorCategory;
            }

            if (request.Status is { } status)
            {
                entry.Status = status;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new UpdateWeakPointCatalogEntryResult(entry.Id, entry.Code, entry.Name, entry.Status);
        }
    }
}
