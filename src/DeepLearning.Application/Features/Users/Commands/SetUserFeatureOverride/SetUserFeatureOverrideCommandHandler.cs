using DeepLearning.Application.Common;
using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.Users.Commands.SetUserFeatureOverride
{
    public class SetUserFeatureOverrideCommandHandler : IRequestHandler<SetUserFeatureOverrideCommand, SetUserFeatureOverrideResult>
    {
        private readonly IUserRepository _userRepository;
        private readonly IUserFeatureOverrideRepository _overrideRepository;
        private readonly IUnitOfWork _unitOfWork;

        public SetUserFeatureOverrideCommandHandler(
            IUserRepository userRepository, IUserFeatureOverrideRepository overrideRepository, IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _overrideRepository = overrideRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<SetUserFeatureOverrideResult> Handle(SetUserFeatureOverrideCommand request, CancellationToken cancellationToken)
        {
            await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
                .EnsureFoundAsync(nameof(Domain.Entities.User), request.UserId);

            if (request.Enabled is { } enabled)
            {
                await _overrideRepository.SetAsync(request.UserId, request.FeatureKey, enabled, cancellationToken);
            }
            else
            {
                await _overrideRepository.ClearAsync(request.UserId, request.FeatureKey, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new SetUserFeatureOverrideResult(request.UserId, request.FeatureKey, request.Enabled);
        }
    }
}
