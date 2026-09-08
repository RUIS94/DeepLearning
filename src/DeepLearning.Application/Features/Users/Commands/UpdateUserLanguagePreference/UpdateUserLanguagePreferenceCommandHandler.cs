using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.Users.Commands.UpdateUserLanguagePreference
{
    public class UpdateUserLanguagePreferenceCommandHandler
        : IRequestHandler<UpdateUserLanguagePreferenceCommand, UpdateUserLanguagePreferenceResult>
    {
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateUserLanguagePreferenceCommandHandler(
            IUserRepository userRepository, IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<UpdateUserLanguagePreferenceResult> Handle(
            UpdateUserLanguagePreferenceCommand request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(Domain.Entities.User), request.Id);

            user.LanguagePreference = request.LanguagePreference;

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new UpdateUserLanguagePreferenceResult(user.Id, user.LanguagePreference);
        }
    }
}
