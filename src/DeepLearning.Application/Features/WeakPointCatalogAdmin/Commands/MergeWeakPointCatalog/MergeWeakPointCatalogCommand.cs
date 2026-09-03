using MediatR;

namespace DeepLearning.Application.Features.WeakPointCatalogAdmin.Commands.MergeWeakPointCatalog
{
    /// <summary>
    /// Folds catalog kind <see cref="FromId"/> into <see cref="ToId"/>: every learner's
    /// weak point on <see cref="FromId"/> is re-pointed to <see cref="ToId"/> (merging when the
    /// learner already has one there), then <see cref="FromId"/> is set to <c>deprecated</c>.
    /// Insert/repoint only — no catalog row is deleted, so history and any lingering references
    /// stay valid.
    /// </summary>
    public record MergeWeakPointCatalogCommand(Guid FromId, Guid ToId)
        : IRequest<MergeWeakPointCatalogResult>;

    public record MergeWeakPointCatalogResult(Guid FromId, Guid ToId, int RepointedCount, int MergedCount);
}
