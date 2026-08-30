using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.StandardOverrides.Queries.ListStandardOverrides
{
    public record StandardOverrideResultItem(
        Guid Id,
        OverrideScope Scope,
        string DimensionOrRule,
        string RevisedRuleText,
        OverrideStatus Status,
        Guid? PreviousOverrideId,
        DateTimeOffset? EffectiveFrom,
        DateTimeOffset CreatedAt);
}
