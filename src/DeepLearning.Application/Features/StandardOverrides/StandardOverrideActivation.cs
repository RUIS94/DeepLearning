using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Events;

namespace DeepLearning.Application.Features.StandardOverrides
{
    /// <summary>
    /// The side effect of activating one StandardOverride row: deprecate whichever row was
    /// previously active for the same rule (if any), flip the target to active, and raise
    /// StandardOverrideActivatedEvent. Previously duplicated between
    /// ActivateStandardOverrideCommandHandler (explicit admin activation) and
    /// CloseFollowUpThreadCommandHandler.TryAutoActivateAsync (auto-activation once enough
    /// independent confirmations land) — see 代码复用扫描_07_优化计划.md §2.5/N3. Each caller still
    /// finds its own "previous" row (by rule vs. by PreviousOverrideId — the two callers look it
    /// up differently) and still owns its own SaveChangesAsync.
    /// </summary>
    public static class StandardOverrideActivation
    {
        public static void Activate(StandardOverride target, StandardOverride? previous, DateTimeOffset now)
        {
            if (previous is not null)
            {
                previous.Status = OverrideStatus.deprecated;
            }

            target.Status = OverrideStatus.active;
            target.EffectiveFrom = now;
            target.AddDomainEvent(new StandardOverrideActivatedEvent
            {
                StandardOverrideId = target.Id,
                Scope = target.Scope,
                DimensionOrRule = target.DimensionOrRule,
                PreviousOverrideId = target.PreviousOverrideId,
                ActivatedAt = target.EffectiveFrom.Value,
            });
        }
    }
}
