using MediatR;

namespace DeepLearning.Application.Features.FeatureFlags.Commands.SetFeatureFlag
{
    public record SetFeatureFlagCommand(string Key, bool Enabled) : IRequest<SetFeatureFlagResult>;

    public record SetFeatureFlagResult(string Key, bool Enabled);
}
