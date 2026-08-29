using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Queries.ListExamTypes
{
    public record ListExamTypesQuery(bool? IsActive) : IRequest<List<ListExamTypesResultItem>>;
}
