using FluentValidation;

namespace DeepLearning.Application.Features.FollowUpThreads.Commands.CreateFollowUpThread
{
    public class CreateFollowUpThreadValidator : AbstractValidator<CreateFollowUpThreadCommand>
    {
        public CreateFollowUpThreadValidator()
        {
            RuleFor(x => x.SubmissionId).NotEmpty();
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.ExamTypeId).NotEmpty();
            RuleFor(x => x.QuestionText).NotEmpty();
            RuleFor(x => x.ContextRef).MaximumLength(100);
        }
    }
}
