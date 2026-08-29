using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Exceptions;

namespace DeepLearning.Infrastructure.Ai
{
    public class ExamConfigLoader : IExamConfigLoader
    {
        private readonly IExamTypeRepository _examTypeRepository;
        private readonly IPromptTemplateRepository _promptTemplateRepository;
        private readonly PromptRenderer _promptRenderer;

        public ExamConfigLoader(
            IExamTypeRepository examTypeRepository,
            IPromptTemplateRepository promptTemplateRepository,
            PromptRenderer promptRenderer)
        {
            _examTypeRepository = examTypeRepository;
            _promptTemplateRepository = promptTemplateRepository;
            _promptRenderer = promptRenderer;
        }

        public async Task<string> BuildPromptAsync(
            Guid examTypeId,
            AiOperationType templateType,
            object templateModel,
            CancellationToken cancellationToken = default)
        {
            var examType = await _examTypeRepository.GetByIdAsync(examTypeId, cancellationToken)
                ?? throw new NotFoundException(nameof(ExamType), examTypeId);

            // shared_methodology rows never carry an exam_type_id, and exam_specific rows
            // never carry a subject_category (enforced by the DB check constraint), so these
            // two calls naturally partition into "shared" and "specific" without filtering
            // by Layer explicitly.
            var sharedTemplates = await _promptTemplateRepository.ListAsync(
                examTypeId: null, subjectCategory: examType.SubjectCategory, templateType: templateType, cancellationToken);
            var specificTemplates = await _promptTemplateRepository.ListAsync(
                examTypeId: examTypeId, subjectCategory: null, templateType: templateType, cancellationToken);

            var segments = sharedTemplates
                .Concat(specificTemplates)
                .Select(t => _promptRenderer.Render(t.TemplateContent, templateModel));

            return string.Join("\n\n", segments);
        }
    }
}
