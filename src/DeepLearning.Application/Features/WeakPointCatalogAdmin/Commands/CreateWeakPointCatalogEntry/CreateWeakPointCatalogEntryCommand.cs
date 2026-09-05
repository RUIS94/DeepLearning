using MediatR;

namespace DeepLearning.Application.Features.WeakPointCatalogAdmin.Commands.CreateWeakPointCatalogEntry
{
    /// <summary>Admin-creates a weak-point catalog leaf (origin = 'manual', status = 'active'), under one of the 8 fixed top-level categories.</summary>
    public record CreateWeakPointCatalogEntryCommand(
        Guid CategoryId,
        string Code,
        string Name,
        string Description,
        string? DefaultDimensionKey,
        string? DefaultErrorCategory) : IRequest<CreateWeakPointCatalogEntryResult>;

    public record CreateWeakPointCatalogEntryResult(Guid Id, Guid CategoryId, string Code, string Name);
}
