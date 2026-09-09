using FluentValidation;

namespace DeepLearning.Application.Features.FeatureFlags.Commands.SetFeatureFlag
{
    public class SetFeatureFlagValidator : AbstractValidator<SetFeatureFlagCommand>
    {
        public SetFeatureFlagValidator()
        {
            // Only the keys the code actually consults — no arbitrary rows from the UI.
            RuleFor(x => x.Key)
                .NotEmpty()
                .Must(Common.FeatureFlags.Defaults.ContainsKey)
                .WithMessage(x => $"'{x.Key}' is not a known feature flag.");
        }
    }
}
