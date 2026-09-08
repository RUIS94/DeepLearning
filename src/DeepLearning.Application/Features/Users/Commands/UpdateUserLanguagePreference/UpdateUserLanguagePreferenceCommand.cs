using MediatR;

namespace DeepLearning.Application.Features.Users.Commands.UpdateUserLanguagePreference
{
    public record UpdateUserLanguagePreferenceCommand(Guid Id, string LanguagePreference)
        : IRequest<UpdateUserLanguagePreferenceResult>;
}
