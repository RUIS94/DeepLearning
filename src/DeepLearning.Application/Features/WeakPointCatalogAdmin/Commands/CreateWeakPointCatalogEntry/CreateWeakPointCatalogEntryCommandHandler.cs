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
        private readonly IExamTypeRepository _examTypeRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateWeakPointCatalogEntryCommandHandler(
            IWeakPointCatalogRepository catalogRepository,
            IExamTypeRepository examTypeRepository,
            IUnitOfWork unitOfWork)
        {
            _catalogRepository = catalogRepository;
            _examTypeRepository = examTypeRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<CreateWeakPointCatalogEntryResult> Handle(CreateWeakPointCatalogEntryCommand request, CancellationToken cancellationToken)
        {
            _ = await _examTypeRepository.GetByIdAsync(request.ExamTypeId, cancellationToken)
                ?? throw new NotFoundException(nameof(ExamType), request.ExamTypeId);

            if (await _catalogRepository.ExistsAsync(request.ExamTypeId, request.Code, cancellationToken))
            {
                throw new ConflictException($"Weak-point catalog code '{request.Code}' already exists for this exam type.");
            }

            var entry = new WeakPointCatalog
            {
                Id = Guid.NewGuid(),
                ExamTypeId = request.ExamTypeId,
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

            return new CreateWeakPointCatalogEntryResult(entry.Id, entry.ExamTypeId, entry.Code, entry.Name);
        }
    }
}
