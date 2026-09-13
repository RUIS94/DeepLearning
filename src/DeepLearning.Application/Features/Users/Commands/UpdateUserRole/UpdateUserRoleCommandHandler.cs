using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.Users.Commands.UpdateUserRole
{
    public class UpdateUserRoleCommandHandler : IRequestHandler<UpdateUserRoleCommand, UpdateUserRoleResult>
    {
        private readonly IUserRepository _userRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateUserRoleCommandHandler(IUserRepository userRepository, IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<UpdateUserRoleResult> Handle(UpdateUserRoleCommand request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(Domain.Entities.User), request.Id);

            if (user.Role == UserRole.admin && request.Role != UserRole.admin)
            {
                var adminCount = await _userRepository.CountByRoleAsync(UserRole.admin, cancellationToken);
                if (adminCount <= 1)
                {
                    throw new ConflictException("At least one admin is required — cannot demote the only remaining admin.");
                }
            }

            user.Role = request.Role;

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new UpdateUserRoleResult(user.Id, user.Role);
        }
    }
}
