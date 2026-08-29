using DeepLearning.Application.Interfaces;
using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Queries.GetPromptTemplatesByExamType
{
    public class GetPromptTemplatesByExamTypeQueryHandler
        : IRequestHandler<GetPromptTemplatesByExamTypeQuery, List<PromptTemplateResultItem>>
    {
        private readonly IPromptTemplateRepository _templateRepository;

        public GetPromptTemplatesByExamTypeQueryHandler(IPromptTemplateRepository templateRepository)
        {
            _templateRepository = templateRepository;
        }

        public async Task<List<PromptTemplateResultItem>> Handle(
            GetPromptTemplatesByExamTypeQuery request,
            CancellationToken cancellationToken)
        {
            var templates = await _templateRepository.ListAsync(
                request.ExamTypeId, request.SubjectCategory, request.TemplateType, cancellationToken);

            return templates.Select(x => new PromptTemplateResultItem(
                x.Id, x.ExamTypeId, x.SubjectCategory, x.TemplateType, x.Layer, x.TemplateContent, x.Version, x.IsActive)).ToList();
        }
    }
}
