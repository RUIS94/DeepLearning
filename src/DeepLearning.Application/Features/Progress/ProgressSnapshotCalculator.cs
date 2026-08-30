using DeepLearning.Domain.Entities;

namespace DeepLearning.Application.Features.Progress
{
    /// <summary>
    /// Pure aggregation of a period's grading_results into the four progress_snapshots measures.
    /// Extracted from UpdateProgressOnGraded (Step 6, which only ever calls this with
    /// periodStart == periodEnd == today) so Step 9's weekly/backfill snapshot generation
    /// (GenerateProgressTrendSnapshotCommandHandler) can reuse the exact same "recompute from
    /// source" logic for an arbitrary period without a second, potentially-drifting
    /// implementation of the same averages.
    /// </summary>
    public static class ProgressSnapshotCalculator
    {
        public record Aggregate(
            decimal? AvgBandMeaningTransfer,
            decimal? AvgBandTextualNorms,
            decimal? AvgBandLanguageProficiency,
            decimal? PassRate);

        public static Aggregate Compute(IReadOnlyCollection<GradingResult> results)
        {
            if (results.Count == 0)
            {
                return new Aggregate(null, null, null, null);
            }

            var submissionGroups = results.GroupBy(x => x.SubmissionId).ToList();
            var passCount = submissionGroups.Count(g => g.All(r => r.PassBool));
            var passRate = Math.Round(100m * passCount / submissionGroups.Count, 2);

            return new Aggregate(
                AverageBand(results, "meaning_transfer"),
                AverageBand(results, "textual_norms"),
                AverageBand(results, "language_proficiency"),
                passRate);
        }

        // dimension_key values are the fixed set seeded for NAATI CT (design doc §6.5) — matches
        // the same convention as other hardcoded rule/policy keys elsewhere in this codebase
        // (e.g. GenerationPolicyRepository's "difficulty_distribution").
        private static decimal? AverageBand(IReadOnlyCollection<GradingResult> results, string dimensionKey)
        {
            var bands = results
                .Where(r => r.Dimension!.DimensionKey == dimensionKey)
                .Select(r => (decimal)r.Band)
                .ToList();

            return bands.Count > 0 ? Math.Round(bands.Average(), 1) : null;
        }
    }
}
