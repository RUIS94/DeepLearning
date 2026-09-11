using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Common
{
    /// <summary>
    /// The `AiCallLog` object-initializer itself — `Id`/`Status=calling`/`AttemptCount=1`/
    /// `MaxRetries=3`/`CreatedAt` are byte-identical across every AiCallLog call site (both the
    /// 4 sites <see cref="AiCallScope"/> consolidates and the ~9 "fail loud, rethrow
    /// AiCallFailedException" sites that don't share its control-flow shape and were deliberately
    /// left untouched — see AiCallScope's doc comment). Only `RequestType`/`RelatedId` vary, so
    /// only the constructor itself is worth a shared factory here — 代码复用扫描_07_优化计划.md
    /// §2.1. `RelatedId` is optional: some callers only know it after a successful AI call and set
    /// `aiCallLog.RelatedId` on the instance afterwards instead of passing it here — that timing is
    /// preserved as-is per call site, not forced through this factory.
    /// </summary>
    public static class AiCallLogFactory
    {
        public static AiCallLog New(AiOperationType requestType, Guid? relatedId = null) => new()
        {
            Id = Guid.NewGuid(),
            RequestType = requestType,
            RelatedId = relatedId,
            Status = CallStatus.calling,
            AttemptCount = 1,
            MaxRetries = 3,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }
}
