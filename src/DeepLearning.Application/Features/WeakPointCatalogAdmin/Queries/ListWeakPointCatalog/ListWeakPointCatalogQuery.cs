using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.WeakPointCatalogAdmin.Queries.ListWeakPointCatalog
{
    /// <summary>Admin view of the weak-point catalog for one exam type — all statuses, optionally filtered.</summary>
    public record ListWeakPointCatalogQuery(Guid ExamTypeId, WeakPointCatalogStatus? Status)
        : IRequest<List<WeakPointCatalogResultItem>>;

    public record WeakPointCatalogResultItem(
        Guid Id,
        Guid ExamTypeId,
        string Code,
        string Name,
        string Description,
        string? DefaultDimensionKey,
        string? DefaultErrorCategory,
        WeakPointCatalogStatus Status,
        string Origin,
        DateTimeOffset CreatedAt);
}
