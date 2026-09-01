using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.StandardOverrides.Commands.DeprecateStandardOverride
{
    public class DeprecateStandardOverrideCommandHandler
        : IRequestHandler<DeprecateStandardOverrideCommand, DeprecateStandardOverrideResult>
    {
        private readonly IStandardOverrideRepository _standardOverrideRepository;
        private readonly IUnitOfWork _unitOfWork;

        public DeprecateStandardOverrideCommandHandler(
            IStandardOverrideRepository standardOverrideRepository, IUnitOfWork unitOfWork)
        {
            _standardOverrideRepository = standardOverrideRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<DeprecateStandardOverrideResult> Handle(
            DeprecateStandardOverrideCommand request, CancellationToken cancellationToken)
        {
            var target = await _standardOverrideRepository.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(StandardOverride), request.Id);

            if (target.Status == OverrideStatus.deprecated)
            {
                throw new ConflictException($"StandardOverride '{target.Id}' is already deprecated.");
            }

            target.Status = OverrideStatus.deprecated;
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new DeprecateStandardOverrideResult(target.Id, target.Status);
        }
    }
}
