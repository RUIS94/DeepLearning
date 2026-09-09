using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Enums;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace DeepLearning.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Design doc §10.6 / §11.2 Step 10: a periodic "calibration report" summarising the
    /// <c>standard_overrides</c> that turned <see cref="OverrideStatus.active"/> in the recent
    /// window — the corrections distilled from user disputes that are now shaping grading. §10.6
    /// wants these sampled against the official rubric text so long-term reliance on user appeals
    /// can't quietly drift the standard away from the official definition.
    ///
    /// <para><b>Minimal skeleton (2026-09-09 Phase 6).</b> This produces the roll-up and writes
    /// it to the structured log — no new table, no migration. The AI cross-check against
    /// <c>assessment_dimensions.level_descriptions</c> is the obvious next step and is marked with
    /// a TODO below; doing it needs a calibration <c>prompt_templates</c> row and a decision on
    /// where the verdict is stored.</para>
    ///
    /// <para><b>No automatic retries</b> (<see cref="GenerateWeakPointsJob"/> policy): it's a
    /// read-only report nobody is blocking on. A fixed trailing window (<see cref="LookbackDays"/>)
    /// rather than a cursor, same "re-running is a harmless no-op" reasoning as
    /// <see cref="ProgressSnapshotJob"/>'s lookback.</para>
    /// </summary>
    [AutomaticRetry(Attempts = 0)]
    public class RubricCalibrationReportJob
    {
        public const int LookbackDays = 7;

        private readonly IStandardOverrideRepository _standardOverrideRepository;
        private readonly ILogger<RubricCalibrationReportJob> _logger;

        public RubricCalibrationReportJob(
            IStandardOverrideRepository standardOverrideRepository,
            ILogger<RubricCalibrationReportJob> logger)
        {
            _standardOverrideRepository = standardOverrideRepository;
            _logger = logger;
        }

        /// <returns>
        /// The number of overrides included in this run's report — a Hangfire job's return value
        /// is ignored at runtime, it's here so the windowing is directly assertable in a test.
        /// </returns>
        public async Task<int> RunAsync(CancellationToken cancellationToken = default)
        {
            var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromDays(LookbackDays);

            var active = await _standardOverrideRepository.ListAsync(OverrideStatus.active, cancellationToken: cancellationToken);
            var recentlyActive = active
                .Where(o => (o.EffectiveFrom ?? o.CreatedAt) >= cutoff)
                .OrderBy(o => o.EffectiveFrom ?? o.CreatedAt)
                .ToList();

            if (recentlyActive.Count == 0)
            {
                _logger.LogInformation(
                    "RubricCalibrationReport: no standard_overrides became active in the last {LookbackDays} day(s). Nothing to review.",
                    LookbackDays);
                return 0;
            }

            var byScope = recentlyActive
                .GroupBy(o => o.Scope)
                .Select(g => $"{g.Key}={g.Count()}");
            _logger.LogWarning(
                "RubricCalibrationReport: {Count} standard_override(s) became active in the last {LookbackDays} day(s) and are due a spot-check against the official rubric. By scope: {ByScope}.",
                recentlyActive.Count, LookbackDays, string.Join(", ", byScope));

            foreach (var o in recentlyActive)
            {
                _logger.LogInformation(
                    "RubricCalibrationReport item: override {OverrideId} scope={Scope} examType={ExamTypeId} rule='{Rule}' effectiveFrom={EffectiveFrom} triggeredBy={TriggeredBy} — revised: {RevisedRuleText}",
                    o.Id,
                    o.Scope,
                    o.ExamTypeId,
                    o.DimensionOrRule,
                    o.EffectiveFrom ?? o.CreatedAt,
                    o.TriggeredByFollowUpThreadId ?? o.TriggeredByFollowupId,
                    Truncate(o.RevisedRuleText, 300));
            }

            // TODO(step10): sample each item against assessment_dimensions.level_descriptions via
            // an AI calibration call (needs a `calibration` prompt_templates row) and record a
            // per-item verdict (aligned / drifted / needs-human) instead of only logging — that's
            // what turns this from a digest into the §10.6 safety mechanism.

            return recentlyActive.Count;
        }

        private static string Truncate(string value, int max)
            => value.Length <= max ? value : value[..max] + "…";
    }
}
