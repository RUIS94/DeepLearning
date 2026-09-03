using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.WeakPoints.Queries.ListWeakPoints
{
    public record WeakPointResultItem(
        Guid Id,
        string Label,
        string? PatternSummary,
        DateTimeOffset FirstDetectedAt,
        DateTimeOffset LastSeenAt,
        int RecurrenceCount,
        WeakPointStatus Status,
        Priority Priority);
}
