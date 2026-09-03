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

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                entry.Name = request.Name;
            }

            if (!string.IsNullOrWhiteSpace(request.Description))
            {
                entry.Description = request.Description;
            }

            // Empty string clears the match key; null leaves it unchanged.
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
