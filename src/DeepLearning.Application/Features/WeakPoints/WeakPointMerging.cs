using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.WeakPoints
{
    /// <summary>
    /// Folds one <see cref="WeakPoint"/> row (<paramref name="source"/>) into another
    /// (<paramref name="target"/>) for the SAME user — used when a reclassify / catalog-merge
    /// lands two of a user's rows on one kind. Mutates <paramref name="target"/> in place and
    /// partitions <paramref name="source"/>'s occurrences into "repoint to target" vs "delete"
    /// (an occurrence whose submission the target already covers would collide with the
    /// ux_weak_point_occurrences_wp_submission unique index). The caller repoints / deletes and
    /// then removes <paramref name="source"/>.
    /// </summary>
    public static class WeakPointMerging
    {
        public static (List<WeakPointOccurrence> Repoint, List<WeakPointOccurrence> Delete) MergeInto(
            WeakPoint source,
            WeakPoint target,
            IReadOnlyList<WeakPointOccurrence> sourceOccurrences,
            ISet<Guid> targetSubmissionIds)
        {
            target.RecurrenceCount += source.RecurrenceCount;
            target.FirstDetectedAt = target.FirstDetectedAt <= source.FirstDetectedAt ? target.FirstDetectedAt : source.FirstDetectedAt;
            target.LastSeenAt = target.LastSeenAt >= source.LastSeenAt ? target.LastSeenAt : source.LastSeenAt;

            // Either one active -> the merged row is active (an unresolved recurrence anywhere wins).
            if (source.Status == WeakPointStatus.active || target.Status == WeakPointStatus.active)
            {
                target.Status = WeakPointStatus.active;
                target.ResolvedAt = null;
            }

            // Priority: high is ordinal 0, so the smaller ordinal is the more urgent one.
            if ((int)source.Priority < (int)target.Priority)
            {
                target.Priority = source.Priority;
            }

            target.PatternSummary ??= source.PatternSummary;
            target.ExamTypeId ??= source.ExamTypeId;

            var repoint = new List<WeakPointOccurrence>();
            var delete = new List<WeakPointOccurrence>();
            foreach (var occurrence in sourceOccurrences)
            {
                if (targetSubmissionIds.Contains(occurrence.SubmissionId))
                {
                    delete.Add(occurrence);
                }
                else
                {
                    occurrence.WeakPointId = target.Id;
                    repoint.Add(occurrence);
                    targetSubmissionIds.Add(occurrence.SubmissionId);
                }
            }

            return (repoint, delete);
        }
    }
}
