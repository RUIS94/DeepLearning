using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Commands.SetExamTypeActivation
{
    public class SetExamTypeActivationCommandHandler
        : IRequestHandler<SetExamTypeActivationCommand, SetExamTypeActivationResult>
    {
        private readonly IExamTypeRepository _examTypeRepository;
        private readonly IUserExamTypeActivationRepository _activationRepository;
        private readonly IUnitOfWork _unitOfWork;

        public SetExamTypeActivationCommandHandler(
            IExamTypeRepository examTypeRepository,
            IUserExamTypeActivationRepository activationRepository,
            IUnitOfWork unitOfWork)
        {
            _examTypeRepository = examTypeRepository;
            _activationRepository = activationRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<SetExamTypeActivationResult> Handle(SetExamTypeActivationCommand request, CancellationToken cancellationToken)
        {
            var examType = await _examTypeRepository.GetByIdAsync(request.ExamTypeId, cancellationToken)
                ?? throw new NotFoundException(nameof(ExamType), request.ExamTypeId);

            // Deactivating is always allowed (e.g. to clear a stale row after the admin turned the
            // exam type off) — only activating requires it to currently be globally available.
            if (request.IsActive && !examType.IsActive)
            {
                throw new ExamTypeInactiveException(request.ExamTypeId);
            }

            await _activationRepository.SetActiveAsync(request.UserId, request.ExamTypeId, request.IsActive, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new SetExamTypeActivationResult(request.ExamTypeId, request.IsActive);
        }
    }
}
