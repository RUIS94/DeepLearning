using FluentValidation;

namespace DeepLearning.Application.Features.Questions.Commands.GenerateQuestion
{
    public class GenerateQuestionValidator : AbstractValidator<GenerateQuestionCommand>
    {
        public GenerateQuestionValidator()
        {
            RuleFor(x => x.ExamTypeId).NotEmpty();
            RuleFor(x => x.TaskType).IsInEnum();
            RuleFor(x => x.Difficulty).IsInEnum();
        }
    }
}
