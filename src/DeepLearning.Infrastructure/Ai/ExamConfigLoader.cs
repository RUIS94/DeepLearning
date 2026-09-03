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

            // The two queries are meant to partition by layer: shared_methodology rows are
            // matched by subject_category, exam_specific rows by exam_type_id. The DB check
            // constraint ck_prompt_templates_layer_scope is an OR, so it does NOT stop a row
            // from carrying BOTH scoping columns — and such a row would then match both
            // queries and be rendered twice (see consolidate_grading_prompt_templates.sql for
            // the incident this guards against). Filter on Layer explicitly so each row lands
            // in exactly one bucket regardless of stray column values.
            var sharedTemplates = (await _promptTemplateRepository.ListAsync(
                    examTypeId: null, subjectCategory: examType.SubjectCategory, templateType: templateType, isActive: true, cancellationToken))
                .Where(t => t.Layer == TemplateLayer.shared_methodology);
            var specificTemplates = (await _promptTemplateRepository.ListAsync(
                    examTypeId: examTypeId, subjectCategory: null, templateType: templateType, isActive: true, cancellationToken))
                .Where(t => t.Layer == TemplateLayer.exam_specific);

            var segments = sharedTemplates
                .Concat(specificTemplates)
                .Select(t => _promptRenderer.Render(t.TemplateContent, templateModel));

            return string.Join("\n\n", segments);
        }
    }
}
