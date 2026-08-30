using DeepLearning.Domain.Enums;
using MediatR;

namespace DeepLearning.Application.Features.ReviewLibrary.Commands.MarkPatternReviewed
{
    /// <summary>
    /// Design doc §2.2 node RD/RE "标记掌握程度 -> 更新user_pattern_review". Upserts: first time
    /// this user reviews this pattern creates the row, a later call just updates
    /// MasteryLevel/LastReviewedAt. TimesEncountered is intentionally left alone here — it's
    /// only ever bumped by ExtractKnowledgePointsOnGraded (Step 6) when the pattern is actually
    /// encountered again during grading, a distinct concept from a manual mastery mark.
    /// </summary>
    public record MarkPatternReviewedCommand(Guid UserId, Guid PatternId, MasteryLevel MasteryLevel) : IRequest<MarkPatternReviewedResult>;
}
