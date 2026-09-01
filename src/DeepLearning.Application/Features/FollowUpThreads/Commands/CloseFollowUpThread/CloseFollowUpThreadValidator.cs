using FluentValidation;

namespace DeepLearning.Application.Features.FollowUpThreads.Commands.CloseFollowUpThread
{
    public class CloseFollowUpThreadValidator : AbstractValidator<CloseFollowUpThreadCommand>
    {
        public CloseFollowUpThreadValidator()
        {
            RuleFor(x => x.ThreadId).NotEmpty();
            RuleFor(x => x.UserId).NotEmpty();
        }
    }
}
