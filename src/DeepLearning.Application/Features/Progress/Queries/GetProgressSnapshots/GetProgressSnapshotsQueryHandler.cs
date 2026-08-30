using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.Progress.Queries.GetProgressSnapshots
{
    public class GetProgressSnapshotsQueryHandler : IRequestHandler<GetProgressSnapshotsQuery, List<ProgressSnapshotResultItem>>
    {
        private readonly IProgressRepository _progressRepository;

        public GetProgressSnapshotsQueryHandler(IProgressRepository progressRepository)
        {
            _progressRepository = progressRepository;
        }

        public async Task<List<ProgressSnapshotResultItem>> Handle(GetProgressSnapshotsQuery request, CancellationToken cancellationToken)
        {
            var snapshots = await _progressRepository.ListByUserAsync(request.UserId, request.DifficultyTier, cancellationToken);

            return snapshots.Select(x => new ProgressSnapshotResultItem(
                x.Id,
                x.PeriodStart,
                x.PeriodEnd,
                x.DifficultyTier,
                x.AvgBandMeaningTransfer,
                x.AvgBandTextualNorms,
                x.AvgBandLanguageProficiency,
                x.PassRate,
                x.TrendNote,
                x.KeyTurningPoint)).ToList();
        }
    }
}
