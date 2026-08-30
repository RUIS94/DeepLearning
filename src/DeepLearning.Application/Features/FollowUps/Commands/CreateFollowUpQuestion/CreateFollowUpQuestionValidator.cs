using FluentValidation;

namespace DeepLearning.Application.Features.FollowUps.Commands.CreateFollowUpQuestion
{
    public class CreateFollowUpQuestionValidator : AbstractValidator<CreateFollowUpQuestionCommand>
    {
        public CreateFollowUpQuestionValidator()
        {
            RuleFor(x => x.SubmissionId).NotEmpty();
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.ExamTypeId).NotEmpty();
            RuleFor(x => x.QuestionText).NotEmpty();
            RuleFor(x => x.ContextRef).MaximumLength(100);
        }
    }
}
