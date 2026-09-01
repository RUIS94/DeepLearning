using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Commands.DeletePromptTemplate
{
    public class DeletePromptTemplateCommandHandler : IRequestHandler<DeletePromptTemplateCommand>
    {
        private readonly IPromptTemplateRepository _templateRepository;
        private readonly IUnitOfWork _unitOfWork;

        public DeletePromptTemplateCommandHandler(
            IPromptTemplateRepository templateRepository, IUnitOfWork unitOfWork)
        {
            _templateRepository = templateRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(DeletePromptTemplateCommand request, CancellationToken cancellationToken)
        {
            var template = await _templateRepository.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(PromptTemplate), request.Id);

            _templateRepository.Remove(template);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
