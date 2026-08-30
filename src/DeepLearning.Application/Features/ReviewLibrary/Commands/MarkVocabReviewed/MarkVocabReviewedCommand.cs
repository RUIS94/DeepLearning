using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.ReviewLibrary.Commands.MarkVocabReviewed
{
    public record MarkVocabReviewedCommand(Guid UserId, Guid VocabId, MasteryLevel MasteryLevel) : IRequest<MarkVocabReviewedResult>;
}
