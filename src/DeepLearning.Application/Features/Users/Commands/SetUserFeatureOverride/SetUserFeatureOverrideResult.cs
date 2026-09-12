namespace DeepLearning.Application.Features.Users.Commands.SetUserFeatureOverride
{
    public record SetUserFeatureOverrideResult(Guid UserId, string FeatureKey, bool? Enabled);
}
