using FluentValidation;

namespace DeepLearning.Application.Features.Questions.Commands.GenerateQuestion
{
    public class GenerateQuestionValidator : AbstractValidator<GenerateQuestionCommand>
    {
        public GenerateQuestionValidator()
        {
            RuleFor(x => x.ExamTypeId).NotEmpty();
            RuleFor(x => x.TaskType).IsInEnum();
            When(x => x.Difficulty.HasValue, () => RuleFor(x => x.Difficulty!.Value).IsInEnum());
        }
    }
}
