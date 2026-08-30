using MediatR;

namespace DeepLearning.Application.Features.StandardOverrides.Queries.GetStandardOverrideById
{
    public record GetStandardOverrideByIdQuery(Guid Id) : IRequest<GetStandardOverrideByIdResult>;
}
