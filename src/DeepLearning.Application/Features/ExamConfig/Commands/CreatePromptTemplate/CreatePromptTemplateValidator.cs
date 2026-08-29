using DeepLearning.Domain.Enums;
using FluentValidation;

namespace DeepLearning.Application.Features.ExamConfig.Commands.CreatePromptTemplate
{
    public class CreatePromptTemplateValidator : AbstractValidator<CreatePromptTemplateCommand>
    {
        public CreatePromptTemplateValidator()
        {
            RuleFor(x => x.TemplateType).IsInEnum();
            RuleFor(x => x.Layer).IsInEnum();
            RuleFor(x => x.TemplateContent).NotEmpty();
            RuleFor(x => x.Version).GreaterThan(0);

            // Mirrors the DB check constraint ck_prompt_templates_layer_scope:
            // exam_specific templates belong to one exam type; shared_methodology
            // templates belong to one subject category — never both, never neither.
            RuleFor(x => x)
                .Must(x => x.Layer == TemplateLayer.exam_specific
                    ? x.ExamTypeId is not null && x.SubjectCategory is null
                    : x.ExamTypeId is null && x.SubjectCategory is not null)
                .WithMessage(
                    "exam_specific templates require ExamTypeId (and no SubjectCategory); " +
                    "shared_methodology templates require SubjectCategory (and no ExamTypeId).")
                .WithName(nameof(CreatePromptTemplateCommand.Layer));
        }
    }
}
