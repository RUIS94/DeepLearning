namespace DeepLearning.Application.Features.Progress.Queries.GetProgressSnapshots
{
    public record ProgressSnapshotResultItem(
        Guid Id,
        DateOnly PeriodStart,
        DateOnly PeriodEnd,
        string? DifficultyTier,
        decimal? AvgBandMeaningTransfer,
        decimal? AvgBandTextualNorms,
        decimal? AvgBandLanguageProficiency,
        decimal? PassRate,
        string? TrendNote,
        bool KeyTurningPoint);
}
