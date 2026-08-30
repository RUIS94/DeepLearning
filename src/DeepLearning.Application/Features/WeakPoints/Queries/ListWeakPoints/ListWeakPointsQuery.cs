using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.WeakPoints.Queries.ListWeakPoints
{
    public record ListWeakPointsQuery(Guid UserId, WeakPointStatus? Status) : IRequest<List<WeakPointResultItem>>;
}
