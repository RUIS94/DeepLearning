using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.StandardOverrides.Commands.ActivateStandardOverride
{
    public record ActivateStandardOverrideResult(Guid Id, OverrideStatus Status, DateTimeOffset? EffectiveFrom);
}
