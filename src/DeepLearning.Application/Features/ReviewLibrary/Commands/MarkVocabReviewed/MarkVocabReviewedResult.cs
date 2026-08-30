using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.ReviewLibrary.Commands.MarkVocabReviewed
{
    public record MarkVocabReviewedResult(
        Guid Id,
        Guid VocabId,
        MasteryLevel MasteryLevel,
        int TimesEncountered,
        DateTimeOffset? LastReviewedAt);
}
