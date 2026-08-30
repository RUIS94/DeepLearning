using MediatR;

namespace DeepLearning.Application.Features.Progress.Queries.GetProgressSnapshots
{
    public record GetProgressSnapshotsQuery(Guid UserId, string? DifficultyTier) : IRequest<List<ProgressSnapshotResultItem>>;
}
