using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.WeakPointCatalogAdmin.Queries.ListWeakPointCatalog
{
    /// <summary>Admin view of the full (global) weak-point catalog — all statuses, optionally filtered.</summary>
    public record ListWeakPointCatalogQuery(WeakPointCatalogStatus? Status)
        : IRequest<List<WeakPointCatalogResultItem>>;

    public record WeakPointCatalogResultItem(
        Guid Id,
        Guid? CategoryId,
        string? CategoryCode,
        string Code,
        string Name,
        string Description,
        string? DefaultDimensionKey,
        string? DefaultErrorCategory,
        WeakPointCatalogStatus Status,
        string Origin,
        DateTimeOffset CreatedAt);
}
