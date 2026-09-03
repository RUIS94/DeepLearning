using MediatR;

namespace DeepLearning.Application.Features.WeakPointCatalogAdmin.Commands.CreateWeakPointCatalogEntry
{
    /// <summary>Admin-creates a weak-point catalog kind (origin = 'manual', status = 'active').</summary>
    public record CreateWeakPointCatalogEntryCommand(
        Guid ExamTypeId,
        string Code,
        string Name,
        string Description,
        string? DefaultDimensionKey,
        string? DefaultErrorCategory) : IRequest<CreateWeakPointCatalogEntryResult>;

    public record CreateWeakPointCatalogEntryResult(Guid Id, Guid ExamTypeId, string Code, string Name);
}
