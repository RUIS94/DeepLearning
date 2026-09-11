using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.WeakPoints.Queries.ListWeakPoints
{
    public record WeakPointResultItem(
        Guid Id,
        /// <summary>Null when the weak point has no catalog kind and no legacy free-text
        /// category — the frontend decides the "uncategorized" label text, not this handler
        /// (代码复用扫描_07_优化计划.md §3.7/N6).</summary>
        string? Label,
        string? PatternSummary,
        DateTimeOffset FirstDetectedAt,
        DateTimeOffset LastSeenAt,
        int RecurrenceCount,
        WeakPointStatus Status,
        Priority Priority);
}
