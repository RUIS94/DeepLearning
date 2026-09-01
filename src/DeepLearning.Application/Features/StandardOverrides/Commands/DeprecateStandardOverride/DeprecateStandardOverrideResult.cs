using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.StandardOverrides.Commands.DeprecateStandardOverride
{
    public record DeprecateStandardOverrideResult(Guid Id, OverrideStatus Status);
}
