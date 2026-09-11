using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Common
{
    /// <summary>
    /// The AiCallLog create → run → success/failure lifecycle, byte-identical across 4
    /// Infrastructure/Ai/*.cs services (VocabSemanticDriftService, WeakPointClassifier,
    /// WeakPointRecheckService, WeakPointDetectionCriteriaGenerator) — 代码复用扫描_07_优化计划.md
    /// §2.1/R-B-4. This is the PoC scope only (the plan's own highest-risk item): the remaining
    /// AiCallLog call sites (e.g. GenerateProgressTrendSnapshotCommandHandler, which also stamps
    /// RelatedId, doesn't swallow a failed failure-log write, and logs a warning before failing)
    /// have real structural differences from these 4 and are deliberately left untouched rather
    /// than forced into this shape. GradeSubmissionCommandHandler is excluded per the plan's own
    /// risk analysis and not touched at all.
    /// </summary>
    public static class AiCallScope
    {
        public static async Task<T> RunAsync<T>(
            IAiCallLogRepository aiCallLogRepository,
            IUnitOfWork unitOfWork,
            AiOperationType requestType,
            Func<AiCallLog, Task<T>> body,
            Func<Exception, T> onFailure,
            string failureMessagePrefix,
            CancellationToken cancellationToken)
        {
            var aiCallLog = AiCallLogFactory.New(requestType);

            try
            {
                await aiCallLogRepository.AddAsync(aiCallLog, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                var result = await body(aiCallLog);

                aiCallLog.Status = CallStatus.success;
                aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
                await unitOfWork.SaveChangesAsync(CancellationToken.None);
                return result;
            }
            catch (Exception ex)
            {
                try
                {
                    aiCallLog.Status = CallStatus.final_failure;
                    aiCallLog.LastErrorMessage = $"{failureMessagePrefix}: {ex.Message}";
                    aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
                    await unitOfWork.SaveChangesAsync(CancellationToken.None);
                }
                catch
                {
                    // ignored — best-effort; never let a failed failure-log write mask the real exception's fallback handling below.
                }

                return onFailure(ex);
            }
        }
    }
}
