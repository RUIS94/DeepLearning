namespace DeepLearning.Application.Features.Progress.Commands.GenerateProgressTrendSnapshot
{
    public record GenerateProgressTrendSnapshotResult(
        Guid? SnapshotId,
        bool Skipped,
        bool TrendNoteGenerated);
}
