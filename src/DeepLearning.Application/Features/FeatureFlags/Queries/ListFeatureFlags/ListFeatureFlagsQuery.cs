using MediatR;

namespace DeepLearning.Application.Features.FeatureFlags.Queries.ListFeatureFlags
{
    /// <summary>
    /// Every known feature flag with its effective value — merges the <c>feature_flags</c> rows
    /// with <see cref="Common.FeatureFlags.Defaults"/> so a key with no row still shows up (at its
    /// documented default). Backs the settings-screen toggles.
    /// </summary>
    public record ListFeatureFlagsQuery : IRequest<List<FeatureFlagResultItem>>;
}
