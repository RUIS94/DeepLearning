using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.ReviewLibrary.Commands.MarkPatternReviewed
{
    public record MarkPatternReviewedResult(
        Guid Id,
        Guid PatternId,
        MasteryLevel MasteryLevel,
        int TimesEncountered,
        DateTimeOffset? LastReviewedAt);
}
