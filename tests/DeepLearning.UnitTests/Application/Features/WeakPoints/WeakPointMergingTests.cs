using DeepLearning.Application.Features.WeakPoints;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;

namespace DeepLearning.UnitTests.Application.Features.WeakPoints
{
    /// <summary>
    /// Pure logic behind ReclassifyWeakPoint / MergeWeakPointCatalog: folding one of a user's
    /// weak-point rows into another. No DB — the handlers add the repo plumbing around this.
    /// </summary>
    public class WeakPointMergingTests
    {
        private static WeakPointOccurrence Occ(Guid weakPointId, Guid submissionId) => new()
        {
            Id = Guid.NewGuid(),
            WeakPointId = weakPointId,
            SubmissionId = submissionId,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        [Fact]
        public void Sums_counts_takes_earliest_first_seen_latest_last_seen_and_more_urgent_priority()
        {
            var early = DateTimeOffset.UtcNow.AddDays(-30);
            var late = DateTimeOffset.UtcNow.AddDays(-1);

            var source = new WeakPoint
            {
                Id = Guid.NewGuid(),
                RecurrenceCount = 2,
                FirstDetectedAt = early,
                LastSeenAt = late,
                Status = WeakPointStatus.active,
                Priority = Priority.high,
                PatternSummary = "source summary",
            };
            var target = new WeakPoint
            {
                Id = Guid.NewGuid(),
                RecurrenceCount = 1,
                FirstDetectedAt = DateTimeOffset.UtcNow.AddDays(-10),
                LastSeenAt = DateTimeOffset.UtcNow.AddDays(-5),
                Status = WeakPointStatus.resolved,
                ResolvedAt = DateTimeOffset.UtcNow.AddDays(-3),
                Priority = Priority.medium,
                PatternSummary = null,
            };

            WeakPointMerging.MergeInto(source, target, [], new HashSet<Guid>());

            Assert.Equal(3, target.RecurrenceCount);
            Assert.Equal(early, target.FirstDetectedAt);
            Assert.Equal(late, target.LastSeenAt);
            Assert.Equal(WeakPointStatus.active, target.Status);
            Assert.Null(target.ResolvedAt);
            Assert.Equal(Priority.high, target.Priority);
            Assert.Equal("source summary", target.PatternSummary);
        }

        [Fact]
        public void Does_not_overwrite_an_existing_target_summary()
        {
            var source = new WeakPoint { Id = Guid.NewGuid(), PatternSummary = "source", Status = WeakPointStatus.active };
            var target = new WeakPoint { Id = Guid.NewGuid(), PatternSummary = "target keeps this", Status = WeakPointStatus.active };

            WeakPointMerging.MergeInto(source, target, [], new HashSet<Guid>());

            Assert.Equal("target keeps this", target.PatternSummary);
        }

        [Fact]
        public void Repoints_occurrences_for_new_submissions_and_drops_ones_that_would_collide()
        {
            var sourceId = Guid.NewGuid();
            var targetId = Guid.NewGuid();
            var sharedSubmission = Guid.NewGuid();
            var uniqueSubmission = Guid.NewGuid();

            var source = new WeakPoint { Id = sourceId, Status = WeakPointStatus.active };
            var target = new WeakPoint { Id = targetId, Status = WeakPointStatus.active };

            var collides = Occ(sourceId, sharedSubmission);
            var moves = Occ(sourceId, uniqueSubmission);

            var (repoint, delete) = WeakPointMerging.MergeInto(
                source, target, [collides, moves], new HashSet<Guid> { sharedSubmission });

            Assert.Equal(new[] { moves }, repoint);
            Assert.Equal(targetId, moves.WeakPointId);
            Assert.Equal(new[] { collides }, delete);
        }
    }
}
