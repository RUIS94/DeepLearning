using FluentValidation;

namespace DeepLearning.Application.Features.Users.Commands.SetUserFeatureOverride
{
    public class SetUserFeatureOverrideValidator : AbstractValidator<SetUserFeatureOverrideCommand>
    {
        public SetUserFeatureOverrideValidator()
        {
            // Only the keys the code actually consults — same convention as SetFeatureFlagValidator.
            RuleFor(x => x.FeatureKey)
                .NotEmpty()
                .Must(Common.FeatureFlags.Defaults.ContainsKey)
                .WithMessage(x => $"'{x.FeatureKey}' is not a known feature flag.");
        }
    }
}
