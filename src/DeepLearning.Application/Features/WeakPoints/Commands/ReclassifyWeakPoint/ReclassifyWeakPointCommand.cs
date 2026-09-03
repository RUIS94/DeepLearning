using MediatR;

namespace DeepLearning.Application.Features.WeakPoints.Commands.ReclassifyWeakPoint
{
    /// <summary>
    /// Manually move one learner's weak point onto a different catalog kind (from a legacy
    /// free-text bucket, or from the wrong kind the rule/AI picked). If the learner already has a
    /// row on the target kind the two are merged. Sets detection_source = 'manual'.
    /// </summary>
    public record ReclassifyWeakPointCommand(Guid WeakPointId, Guid CatalogId)
        : IRequest<ReclassifyWeakPointResult>;

    public record ReclassifyWeakPointResult(Guid WeakPointId, Guid CatalogId, bool MergedIntoExisting);
}
