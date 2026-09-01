using FluentValidation;

namespace DeepLearning.Application.Features.ExamConfig.Commands.UpdatePromptTemplate
{
    public class UpdatePromptTemplateValidator : AbstractValidator<UpdatePromptTemplateCommand>
    {
        public UpdatePromptTemplateValidator()
        {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.TemplateContent).NotEmpty();
            RuleFor(x => x.Version).GreaterThan(0);
        }
    }
}
