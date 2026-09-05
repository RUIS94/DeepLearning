using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.WeakPointCatalogAdmin.Commands.CreateWeakPointCatalogEntry
{
    public class CreateWeakPointCatalogEntryCommandHandler
        : IRequestHandler<CreateWeakPointCatalogEntryCommand, CreateWeakPointCatalogEntryResult>
    {
        private readonly IWeakPointCatalogRepository _catalogRepository;
        private readonly IWeakPointCategoryRepository _categoryRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateWeakPointCatalogEntryCommandHandler(
            IWeakPointCatalogRepository catalogRepository,
            IWeakPointCategoryRepository categoryRepository,
            IUnitOfWork unitOfWork)
        {
            _catalogRepository = catalogRepository;
            _categoryRepository = categoryRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<CreateWeakPointCatalogEntryResult> Handle(CreateWeakPointCatalogEntryCommand request, CancellationToken cancellationToken)
        {
            _ = await _categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken)
                ?? throw new NotFoundException(nameof(WeakPointCategory), request.CategoryId);

            if (await _catalogRepository.ExistsAsync(request.Code, cancellationToken))
            {
                throw new ConflictException($"Weak-point catalog code '{request.Code}' already exists.");
            }

            var entry = new WeakPointCatalog
            {
                Id = Guid.NewGuid(),
                CategoryId = request.CategoryId,
                Code = request.Code,
                Name = request.Name,
                Description = request.Description,
                DefaultDimensionKey = string.IsNullOrWhiteSpace(request.DefaultDimensionKey) ? null : request.DefaultDimensionKey,
                DefaultErrorCategory = string.IsNullOrWhiteSpace(request.DefaultErrorCategory) ? null : request.DefaultErrorCategory,
                Status = WeakPointCatalogStatus.active,
                Origin = "manual",
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await _catalogRepository.AddAsync(entry, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new CreateWeakPointCatalogEntryResult(entry.Id, entry.CategoryId!.Value, entry.Code, entry.Name);
        }
    }
}
