using FluentValidation;

namespace DeepLearning.Application.Features.LlmProviders.Commands.AddLlmProviderModel
{
    public class AddLlmProviderModelValidator : AbstractValidator<AddLlmProviderModelCommand>
    {
        public AddLlmProviderModelValidator()
        {
            RuleFor(x => x.ProviderKey).NotEmpty();
            RuleFor(x => x.Model).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Label).MaximumLength(100);
        }
    }
}
