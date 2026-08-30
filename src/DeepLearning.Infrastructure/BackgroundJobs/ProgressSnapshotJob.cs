using DeepLearning.Application.Features.Progress;
using DeepLearning.Application.Features.Progress.Commands.GenerateProgressTrendSnapshot;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DeepLearning.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Design doc §11.2 Step 9: "progress_snapshots, Hangfire定时任务生成快照" — registered as a
    /// weekly Hangfire recurring job (Program.cs: RecurringJob.AddOrUpdate&lt;ProgressSnapshotJob&gt;).
    /// This class is deliberately thin — pure iteration, no business logic — matching AGENTS.md's
    /// "Controllers only bind parameters and call IMediator.Send" convention, extended here to the
    /// one other entry point this codebase has: a scheduled job is just a different trigger for
    /// the same CQRS command as an HTTP request would be. All the actual recompute/AI-narrative
    /// work lives in GenerateProgressTrendSnapshotCommandHandler, independently unit/integration
    /// testable without Hangfire ever running.
    ///
    /// One MediatR command per (active exam type, active user, difficulty tier, trailing week) —
    /// covers both "this week's fresh snapshot" and "backfill any of the last few weeks that
    /// don't have a row yet" with the same call, since the handler recomputes from source
    /// grading_results rather than incrementing, so re-running it for an already-populated week
    /// is a safe, idempotent no-op update. Deliberately bounded to a trailing lookback window
    /// (LookbackWeeks) rather than a user's entire history — an unbounded historical scan on a
    /// job that runs every week would grow more expensive from run to run for no product benefit,
    /// since every prior week already got its own snapshot the first time this job ever saw it.
    /// </summary>
    public class ProgressSnapshotJob
    {
        private const int LookbackWeeks = 12;

        private readonly IExamTypeRepository _examTypeRepository;
        private readonly IProgressRepository _progressRepository;
        private readonly IMediator _mediator;
        private readonly ILogger<ProgressSnapshotJob> _logger;

        public ProgressSnapshotJob(
            IExamTypeRepository examTypeRepository,
            IProgressRepository progressRepository,
            IMediator mediator,
            ILogger<ProgressSnapshotJob> logger)
        {
            _examTypeRepository = examTypeRepository;
            _progressRepository = progressRepository;
            _mediator = mediator;
            _logger = logger;
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var weeks = ProgressWeekCalculator.TrailingWeeks(today, LookbackWeeks);
            var since = weeks[0].PeriodStart;

            var examTypes = await _examTypeRepository.ListAsync(isActive: true, cancellationToken);
            var userIds = await _progressRepository.ListUserIdsWithGradingActivitySinceAsync(since, cancellationToken);
            var difficultyTiers = Enum.GetValues<Difficulty>().Select(d => d.ToString()).ToList();

            var processed = 0;
            var failed = 0;

            foreach (var examType in examTypes)
            {
                foreach (var userId in userIds)
                {
                    foreach (var difficultyTier in difficultyTiers)
                    {
                        foreach (var week in weeks)
                        {
                            try
                            {
                                await _mediator.Send(
                                    new GenerateProgressTrendSnapshotCommand(userId, examType.Id, difficultyTier, week.PeriodStart, week.PeriodEnd),
                                    cancellationToken);
                                processed++;
                            }
                            catch (Exception ex)
                            {
                                // One user/tier/week failing (e.g. a transient DB error) must not
                                // abort the whole weekly batch for every other user — logged and
                                // skipped, same "isolate the blast radius of one bad unit" spirit
                                // as the handler's own AI-failure handling.
                                failed++;
                                _logger.LogError(ex,
                                    "ProgressSnapshotJob failed for user {UserId}, tier {DifficultyTier}, week {PeriodStart}",
                                    userId, difficultyTier, week.PeriodStart);
                            }
                        }
                    }
                }
            }

            _logger.LogInformation(
                "ProgressSnapshotJob completed: {Processed} unit(s) processed, {Failed} failed, {UserCount} user(s), {ExamTypeCount} exam type(s).",
                processed, failed, userIds.Count, examTypes.Count);
        }
    }
}
