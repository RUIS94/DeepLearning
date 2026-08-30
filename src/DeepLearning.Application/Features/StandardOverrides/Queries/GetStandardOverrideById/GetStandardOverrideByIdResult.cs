using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.StandardOverrides.Queries.GetStandardOverrideById
{
    public record GetStandardOverrideByIdResult(
        Guid Id,
        OverrideScope Scope,
        string DimensionOrRule,
        string? OriginalRuleText,
        string RevisedRuleText,
        Guid? TriggeredByFollowupId,
        OverrideStatus Status,
        Guid? PreviousOverrideId,
        DateTimeOffset? EffectiveFrom,
        DateTimeOffset CreatedAt);
}
