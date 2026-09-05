using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.WeakPoints.Commands.ReclassifyWeakPoint
{
    public class ReclassifyWeakPointCommandHandler
        : IRequestHandler<ReclassifyWeakPointCommand, ReclassifyWeakPointResult>
    {
        private readonly IWeakPointRepository _weakPointRepository;
        private readonly IWeakPointCatalogRepository _catalogRepository;
        private readonly IUnitOfWork _unitOfWork;

        public ReclassifyWeakPointCommandHandler(
            IWeakPointRepository weakPointRepository,
            IWeakPointCatalogRepository catalogRepository,
            IUnitOfWork unitOfWork)
        {
            _weakPointRepository = weakPointRepository;
            _catalogRepository = catalogRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<ReclassifyWeakPointResult> Handle(ReclassifyWeakPointCommand request, CancellationToken cancellationToken)
        {
            var weakPoint = await _weakPointRepository.GetByIdAsync(request.WeakPointId, cancellationToken)
                ?? throw new NotFoundException(nameof(WeakPoint), request.WeakPointId);

            var catalog = await _catalogRepository.GetByIdAsync(request.CatalogId, cancellationToken)
                ?? throw new NotFoundException(nameof(WeakPointCatalog), request.CatalogId);

            if (catalog.Status == WeakPointCatalogStatus.deprecated)
            {
                throw new ConflictException($"Catalog kind '{catalog.Code}' is deprecated — reclassify into an active kind instead.");
            }

            if (weakPoint.CatalogId == request.CatalogId)
            {
                return new ReclassifyWeakPointResult(weakPoint.Id, request.CatalogId, MergedIntoExisting: false);
            }

            var existingTarget = await _weakPointRepository.GetByUserAndCatalogAsync(
                weakPoint.UserId, request.CatalogId, cancellationToken);

            bool merged;
            if (existingTarget is not null && existingTarget.Id != weakPoint.Id)
            {
                var sourceOccurrences = await _weakPointRepository.ListOccurrencesByWeakPointAsync(weakPoint.Id, cancellationToken);
                var targetOccurrences = await _weakPointRepository.ListOccurrencesByWeakPointAsync(existingTarget.Id, cancellationToken);
                var targetSubmissionIds = targetOccurrences.Select(o => o.SubmissionId).ToHashSet();

                var (_, delete) = WeakPointMerging.MergeInto(weakPoint, existingTarget, sourceOccurrences, targetSubmissionIds);
                foreach (var occurrence in delete)
                {
                    _weakPointRepository.RemoveOccurrence(occurrence);
                }

                existingTarget.DetectionSource = "manual";
                _weakPointRepository.RemoveWeakPoint(weakPoint);
                merged = true;
            }
            else
            {
                weakPoint.CatalogId = request.CatalogId;
                weakPoint.Category = null;
                weakPoint.DetectionSource = "manual";
                merged = false;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new ReclassifyWeakPointResult(request.WeakPointId, request.CatalogId, merged);
        }
    }
}
