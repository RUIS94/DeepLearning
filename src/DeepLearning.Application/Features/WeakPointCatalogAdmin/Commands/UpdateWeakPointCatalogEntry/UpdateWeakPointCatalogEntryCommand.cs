using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.WeakPointCatalogAdmin.Commands.UpdateWeakPointCatalogEntry
{
    /// <summary>
    /// Edits a catalog kind. Partial: null fields are left unchanged. Covers approving a
    /// <c>proposed</c> row (Status -&gt; active), renaming, retiring (Status -&gt; deprecated),
    /// and adjusting the default (dimension, error-category) match keys. Code is immutable
    /// (it is the row's identity and may be referenced by minted proposals).
    /// </summary>
    public record UpdateWeakPointCatalogEntryCommand(
        Guid Id,
        string? Name,
        string? Description,
        string? DefaultDimensionKey,
        string? DefaultErrorCategory,
        WeakPointCatalogStatus? Status) : IRequest<UpdateWeakPointCatalogEntryResult>;

    public record UpdateWeakPointCatalogEntryResult(Guid Id, string Code, string Name, WeakPointCatalogStatus Status);
}
