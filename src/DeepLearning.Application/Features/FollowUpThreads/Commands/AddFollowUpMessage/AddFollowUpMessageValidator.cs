using FluentValidation;

namespace DeepLearning.Application.Features.FollowUpThreads.Commands.AddFollowUpMessage
{
    public class AddFollowUpMessageValidator : AbstractValidator<AddFollowUpMessageCommand>
    {
        public AddFollowUpMessageValidator()
        {
            RuleFor(x => x.ThreadId).NotEmpty();
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.QuestionText).NotEmpty();
        }
    }
}
