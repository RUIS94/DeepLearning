using MediatR;

namespace DeepLearning.Application.Features.Questions.Queries.GetQuestionById
{
    public record GetQuestionByIdQuery(Guid Id) : IRequest<GetQuestionByIdResult>;
}
