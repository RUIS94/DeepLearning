using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Queries.GetErrorTaxonomiesByExamType
{
    public record GetErrorTaxonomiesByExamTypeQuery(Guid ExamTypeId) : IRequest<List<ErrorTaxonomyResultItem>>;
}
