using DeepLearning.Application.Features.WeakPoints;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.WeakPointCatalogAdmin.Commands.MergeWeakPointCatalog
{
    public class MergeWeakPointCatalogCommandHandler
        : IRequestHandler<MergeWeakPointCatalogCommand, MergeWeakPointCatalogResult>
    {
        private readonly IWeakPointCatalogRepository _catalogRepository;
        private readonly IWeakPointRepository _weakPointRepository;
        private readonly IUnitOfWork _unitOfWork;

        public MergeWeakPointCatalogCommandHandler(
            IWeakPointCatalogRepository catalogRepository,
            IWeakPointRepository weakPointRepository,
            IUnitOfWork unitOfWork)
        {
            _catalogRepository = catalogRepository;
            _weakPointRepository = weakPointRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<MergeWeakPointCatalogResult> Handle(MergeWeakPointCatalogCommand request, CancellationToken cancellationToken)
        {
            if (request.FromId == request.ToId)
            {
                throw new ConflictException("Cannot merge a catalog kind into itself.");
            }

            var from = await _catalogRepository.GetByIdAsync(request.FromId, cancellationToken)
                ?? throw new NotFoundException(nameof(WeakPointCatalog), request.FromId);
            var to = await _catalogRepository.GetByIdAsync(request.ToId, cancellationToken)
                ?? throw new NotFoundException(nameof(WeakPointCatalog), request.ToId);

            if (from.ExamTypeId != to.ExamTypeId)
            {
                throw new ConflictException("The two catalog kinds belong to different exam types.");
            }

            if (to.Status == WeakPointCatalogStatus.deprecated)
            {
                throw new ConflictException($"Merge target '{to.Code}' is deprecated.");
            }

            var fromWeakPoints = await _weakPointRepository.ListByCatalogIdAsync(request.FromId, cancellationToken);
            var toByUser = (await _weakPointRepository.ListByCatalogIdAsync(request.ToId, cancellationToken))
                .ToDictionary(w => w.UserId);

            var repointed = 0;
            var mergedCount = 0;
            foreach (var weakPoint in fromWeakPoints)
            {
                if (toByUser.TryGetValue(weakPoint.UserId, out var target) && target.Id != weakPoint.Id)
                {
                    var sourceOccurrences = await _weakPointRepository.ListOccurrencesByWeakPointAsync(weakPoint.Id, cancellationToken);
                    var targetOccurrences = await _weakPointRepository.ListOccurrencesByWeakPointAsync(target.Id, cancellationToken);
                    var targetSubmissionIds = targetOccurrences.Select(o => o.SubmissionId).ToHashSet();

                    var (_, delete) = WeakPointMerging.MergeInto(weakPoint, target, sourceOccurrences, targetSubmissionIds);
                    foreach (var occurrence in delete)
                    {
                        _weakPointRepository.RemoveOccurrence(occurrence);
                    }

                    _weakPointRepository.RemoveWeakPoint(weakPoint);
                    mergedCount++;
                }
                else
                {
                    weakPoint.CatalogId = request.ToId;
                    weakPoint.Category = null;
                    toByUser[weakPoint.UserId] = weakPoint;
                    repointed++;
                }
            }

            from.Status = WeakPointCatalogStatus.deprecated;

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new MergeWeakPointCatalogResult(request.FromId, request.ToId, repointed, mergedCount);
        }
    }
}
