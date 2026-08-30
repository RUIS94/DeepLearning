using MediatR;

namespace DeepLearning.Application.Features.StandardOverrides.Commands.ActivateStandardOverride
{
    /// <summary>
    /// Design doc §10.6's "或经过一次人工复核" path — promotes an observing row to active
    /// regardless of whether the confirmation-count threshold (StandardOverrideActivationPolicy)
    /// has been reached yet. The automatic count-based path lives in
    /// CreateFollowUpQuestionCommandHandler.TryAutoActivateAsync; this is the manual alternative.
    /// </summary>
    public record ActivateStandardOverrideCommand(Guid Id) : IRequest<ActivateStandardOverrideResult>;
}
