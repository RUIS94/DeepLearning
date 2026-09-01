using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Commands.UpdatePromptTemplate
{
    public class UpdatePromptTemplateCommandHandler
        : IRequestHandler<UpdatePromptTemplateCommand, UpdatePromptTemplateResult>
    {
        private readonly IPromptTemplateRepository _templateRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdatePromptTemplateCommandHandler(
            IPromptTemplateRepository templateRepository, IUnitOfWork unitOfWork)
        {
            _templateRepository = templateRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<UpdatePromptTemplateResult> Handle(
            UpdatePromptTemplateCommand request, CancellationToken cancellationToken)
        {
            var template = await _templateRepository.GetByIdAsync(request.Id, cancellationToken)
                ?? throw new NotFoundException(nameof(PromptTemplate), request.Id);

            template.TemplateContent = request.TemplateContent;
            template.Version = request.Version;
            template.IsActive = request.IsActive;

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new UpdatePromptTemplateResult(
                template.Id, template.ExamTypeId, template.SubjectCategory, template.TemplateType,
                template.Layer, template.TemplateContent, template.Version, template.IsActive);
        }
    }
}
