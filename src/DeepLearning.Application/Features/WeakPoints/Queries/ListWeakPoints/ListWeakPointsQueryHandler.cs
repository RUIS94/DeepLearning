using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.WeakPoints.Queries.ListWeakPoints
{
    public class ListWeakPointsQueryHandler : IRequestHandler<ListWeakPointsQuery, List<WeakPointResultItem>>
    {
        private readonly IWeakPointRepository _weakPointRepository;

        public ListWeakPointsQueryHandler(IWeakPointRepository weakPointRepository)
        {
            _weakPointRepository = weakPointRepository;
        }

        public async Task<List<WeakPointResultItem>> Handle(ListWeakPointsQuery request, CancellationToken cancellationToken)
        {
            var weakPoints = await _weakPointRepository.ListByUserAsync(request.UserId, request.Status, cancellationToken);

            return weakPoints.Select(x => new WeakPointResultItem(
                x.Id,
                x.Category,
                x.Description,
                x.FirstDetectedAt,
                x.LastSeenAt,
                x.RecurrenceCount,
                x.Status,
                x.Priority)).ToList();
        }
    }
}
