using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Commands.CreatePromptTemplate
{
    public class CreatePromptTemplateCommandHandler
        : IRequestHandler<CreatePromptTemplateCommand, CreatePromptTemplateResult>
    {
        private readonly IPromptTemplateRepository _templateRepository;
        private readonly IExamTypeRepository _examTypeRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreatePromptTemplateCommandHandler(
            IPromptTemplateRepository templateRepository,
            IExamTypeRepository examTypeRepository,
            IUnitOfWork unitOfWork)
        {
            _templateRepository = templateRepository;
            _examTypeRepository = examTypeRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<CreatePromptTemplateResult> Handle(
            CreatePromptTemplateCommand request,
            CancellationToken cancellationToken)
        {
            if (request.ExamTypeId is { } examTypeId)
            {
                _ = await _examTypeRepository.GetByIdAsync(examTypeId, cancellationToken)
                    ?? throw new NotFoundException(nameof(ExamType), examTypeId);
            }

            var template = new PromptTemplate
            {
                Id = Guid.NewGuid(),
                ExamTypeId = request.ExamTypeId,
                SubjectCategory = request.SubjectCategory,
                TemplateType = request.TemplateType,
                Layer = request.Layer,
                TemplateContent = request.TemplateContent,
                Version = request.Version,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            await _templateRepository.AddAsync(template, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new CreatePromptTemplateResult(template.Id, template.TemplateType, template.Layer, template.Version, template.IsActive);
        }
    }
}
