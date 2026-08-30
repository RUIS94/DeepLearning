using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.StandardOverrides.Commands.ActivateStandardOverride
{
    public class ActivateStandardOverrideCommandHandler : IRequestHandler<ActivateStandardOverrideCommand, ActivateStandardOverrideResult>
    {
        private readonly IStandardOverrideRepository _standardOverrideRepository;
        private readonly IUnitOfWork _unitOfWork;

        public ActivateStandardOverrideCommandHandler(IStandardOverrideRepository standardOverrideRepository, IUnitOfWork unitOfWork)
        {
            _standardOverrideRepository = standardOverrideRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<ActivateStandardOverrideResult> Handle(ActivateStandardOverrideCommand request, CancellationToken cancellationToken)
        {
            var target = await _standardOverrideRepository.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(StandardOverride), request.Id);

            if (target.Status != OverrideStatus.observing)
            {
                throw new ConflictException($"StandardOverride '{target.Id}' is '{target.Status}', only an 'observing' row can be activated.");
            }

            // No partial-unique-index concern here (unlike ActivateLlmProviderCommand) — status
            // is a plain indexed column, not a uniqueness constraint — so both changes go in one save.
            var previousActive = await _standardOverrideRepository.GetActiveByRuleAsync(target.Scope, target.DimensionOrRule, cancellationToken);
            if (previousActive is not null)
            {
                previousActive.Status = OverrideStatus.deprecated;
            }

            target.Status = OverrideStatus.active;
            target.EffectiveFrom = DateTimeOffset.UtcNow;

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ActivateStandardOverrideResult(target.Id, target.Status, target.EffectiveFrom);
        }
    }
}
