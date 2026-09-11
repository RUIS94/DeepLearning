using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Common;

namespace DeepLearning.Application.Common
{
    /// <summary>
    /// Flip a single boolean "the active/current one" flag across a set of sibling rows — a no-op
    /// if the target is already the one, otherwise flip every other true sibling off, save, then
    /// flip the target on, save. Previously duplicated between ActivateLlmProviderCommandHandler
    /// and SelectLlmProviderModelCommandHandler (代码复用扫描_07_优化计划.md §2.4/N2).
    ///
    /// Two separate saves, deliberately: both callers have a partial unique index (WHERE flag =
    /// true) that Postgres checks per-statement, not deferred, so setting the new row true before
    /// every old one is false would transiently violate it within the same transaction.
    /// </summary>
    public static class ExclusiveFlag
    {
        public static async Task SetAsync<T>(
            IReadOnlyList<T> siblings,
            T target,
            Func<T, bool> get,
            Action<T, bool> set,
            IUnitOfWork unitOfWork,
            CancellationToken cancellationToken)
            where T : Entity
        {
            if (get(target))
            {
                return;
            }

            foreach (var other in siblings.Where(x => get(x) && x.Id != target.Id))
            {
                set(other, false);
            }
            await unitOfWork.SaveChangesAsync(cancellationToken);

            set(target, true);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
