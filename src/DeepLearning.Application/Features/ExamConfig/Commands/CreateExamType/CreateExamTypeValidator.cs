using FluentValidation;

namespace DeepLearning.Application.Features.ExamConfig.Commands.CreateExamType
{
    public class CreateExamTypeValidator : AbstractValidator<CreateExamTypeCommand>
    {
        public CreateExamTypeValidator()
        {
            RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.SubjectCategory).IsInEnum();
            RuleFor(x => x.SourceLanguage).MaximumLength(20);
            RuleFor(x => x.TargetLanguage).MaximumLength(20);
            RuleFor(x => x.GradeLevel).MaximumLength(50);
        }
    }
}
