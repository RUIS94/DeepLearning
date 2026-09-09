using DeepLearning.Application.Features.Submissions.Commands.GradeSubmission;
using DeepLearning.Application.Interfaces;
using Hangfire;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DeepLearning.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Design doc §7 "Prompt回归测试" / §11.3: after a <c>prompt_templates</c> row changes, re-grade
    /// a batch of historical submissions with the new prompt and check whether the Band
    /// distribution drifted — so a multi-type prompt edit can't silently move the grading
    /// standard without anyone noticing.
    ///
    /// <para><b>Minimal skeleton (2026-09-09 Phase 7).</b> It samples the most recent graded
    /// submissions and reports their <em>current</em> per-dimension Band distribution. With
    /// <c>dryRun = true</c> (the default) it stops there — no LLM calls, no cost. With
    /// <c>dryRun = false</c> it re-grades each via <see cref="GradeSubmissionCommand"/> and diffs
    /// the two distributions with a crude per-band-count threshold. What's still missing before
    /// this is a real CI gate is marked with a <c>TODO(step10)</c> below: a stored baseline
    /// distribution to diff against, a nightly <c>LlmIntegration</c> job, and a PR hook.</para>
    ///
    /// <para><b>Not registered as a recurring job</b> — it's triggered on demand (the
    /// <c>prompt-regression</c> CLI verb, or Hangfire's dashboard). <b>No automatic retries.</b></para>
    /// </summary>
    [AutomaticRetry(Attempts = 0)]
    public class PromptRegressionTestJob
    {
        public const int DefaultSampleSize = 20;

        /// <summary>A dimension's band-count moving by more than this between runs is flagged as drift.</summary>
        private const int DriftThreshold = 3;

        private readonly ISubmissionRepository _submissionRepository;
        private readonly IMediator _mediator;
        private readonly ILogger<PromptRegressionTestJob> _logger;

        public PromptRegressionTestJob(
            ISubmissionRepository submissionRepository,
            IMediator mediator,
            ILogger<PromptRegressionTestJob> logger)
        {
            _submissionRepository = submissionRepository;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task<PromptRegressionReport> RunAsync(
            Guid examTypeId,
            int sampleSize = DefaultSampleSize,
            bool dryRun = true,
            CancellationToken cancellationToken = default)
        {
            var ids = await _submissionRepository.ListRecentGradedIdsAsync(Math.Max(1, sampleSize), cancellationToken);

            var before = new BandHistogram();
            foreach (var id in ids)
            {
                foreach (var result in await _submissionRepository.GetGradingResultsAsync(id, cancellationToken))
                {
                    before.Add(result.Dimension?.DimensionKey ?? "(unknown)", result.Band);
                }
            }

            _logger.LogInformation(
                "PromptRegression: current Band distribution over {Count} recent graded submission(s) (dryRun={DryRun}): {Distribution}",
                ids.Count, dryRun, before.Describe());

            if (dryRun)
            {
                _logger.LogInformation(
                    "PromptRegression: dry run — pass dryRun=false to actually re-grade these {Count} submission(s) with the current prompt (real LLM cost) and diff the distribution.",
                    ids.Count);
                return new PromptRegressionReport(ids.Count, before.Snapshot(), After: null, DriftDetected: false);
            }

            // TODO(step10): wire into a nightly LlmIntegration job with a stored baseline
            // distribution to diff against, plus a PR hook that runs it whenever a
            // prompt_templates row for this exam type changes (design doc §7 / §11.3).
            var after = new BandHistogram();
            var reGraded = 0;
            foreach (var id in ids)
            {
                try
                {
                    await _mediator.Send(new GradeSubmissionCommand(id, examTypeId), cancellationToken);
                    reGraded++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PromptRegression: re-grade failed for submission {SubmissionId}, skipping.", id);
                    continue;
                }

                foreach (var result in await _submissionRepository.GetGradingResultsAsync(id, cancellationToken))
                {
                    after.Add(result.Dimension?.DimensionKey ?? "(unknown)", result.Band);
                }
            }

            var drift = before.MaxPerBandDelta(after) > DriftThreshold;
            _logger.LogWarning(
                "PromptRegression: re-graded {ReGraded}/{Count}; before={Before} after={After} driftDetected={Drift}",
                reGraded, ids.Count, before.Describe(), after.Describe(), drift);

            return new PromptRegressionReport(ids.Count, before.Snapshot(), after.Snapshot(), drift);
        }

        /// <summary>Per-dimension count of each Band 1-5.</summary>
        private sealed class BandHistogram
        {
            private readonly Dictionary<string, int[]> _byDimension = new();

            public void Add(string dimensionKey, int band)
            {
                if (band is < 1 or > 5)
                {
                    return;
                }

                if (!_byDimension.TryGetValue(dimensionKey, out var counts))
                {
                    counts = new int[5];
                    _byDimension[dimensionKey] = counts;
                }

                counts[band - 1]++;
            }

            public IReadOnlyDictionary<string, int[]> Snapshot()
                => _byDimension.ToDictionary(kv => kv.Key, kv => (int[])kv.Value.Clone());

            public string Describe()
                => _byDimension.Count == 0
                    ? "(no graded results)"
                    : string.Join("; ", _byDimension.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}[{string.Join(",", kv.Value)}]"));

            public int MaxPerBandDelta(BandHistogram other)
            {
                var max = 0;
                foreach (var key in _byDimension.Keys.Union(other._byDimension.Keys))
                {
                    var a = _byDimension.GetValueOrDefault(key) ?? new int[5];
                    var b = other._byDimension.GetValueOrDefault(key) ?? new int[5];
                    for (var i = 0; i < 5; i++)
                    {
                        max = Math.Max(max, Math.Abs(a[i] - b[i]));
                    }
                }

                return max;
            }
        }
    }

    /// <param name="SampledCount">How many submissions were in the sample.</param>
    /// <param name="Before">Current per-dimension Band histogram (`int[5]` per dimension key).</param>
    /// <param name="After">Post-re-grade histogram — null on a dry run.</param>
    /// <param name="DriftDetected">A per-band count moved by more than the threshold (dry run: always false).</param>
    public record PromptRegressionReport(
        int SampledCount,
        IReadOnlyDictionary<string, int[]> Before,
        IReadOnlyDictionary<string, int[]>? After,
        bool DriftDetected);
}
