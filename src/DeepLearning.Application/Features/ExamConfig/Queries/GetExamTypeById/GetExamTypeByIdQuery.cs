using MediatR;

namespace DeepLearning.Application.Features.ExamConfig.Queries.GetExamTypeById
{
    public record GetExamTypeByIdQuery(Guid Id) : IRequest<GetExamTypeByIdResult>;
}
