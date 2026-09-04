using DeepLearning.Domain.Enums;
using DeepLearning.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeepLearning.Infrastructure.BackgroundJobs
{
    /// <summary>
    /// Releases submissions stranded in <see cref="SubmissionStatus.grading"/> back to
    /// <see cref="SubmissionStatus.grading_failed"/>, which is the only status a re-grade can
    /// legally start from.
    ///
    /// <para><b>Why this exists.</b> Grading commits the submission to Grading before it makes
    /// any LLM call, precisely so the in-progress state survives a crash. But the state machine
    /// has no Grading -> Grading transition, so if the process dies between those two points
    /// nothing can ever move the row again: the API refuses to re-grade it, and there is no UI
    /// path out. It happened twice on 2026-09-04 — once when the API process was killed
    /// mid-run, once when a cancelled request made the failure handler itself throw — and both
    /// times the only fix was hand-written UPDATE statements against production.
    /// GradeSubmissionCommandHandler now handles the cancellation case itself (AGENTS.md #13),
    /// but nothing in-process can cover a hard kill, so this sweep is the backstop.</para>
    ///
    /// <para>Deliberately a repair job, not a retry job: it does not re-run the grading, it only
    /// makes retrying possible again and leaves the decision to the user. Re-running
    /// automatically would spend real tokens on a submission nobody is waiting for.</para>
    ///
    /// <para>Staleness is judged from the AI call log rather than the clock alone: a row only
    /// counts as stranded when its grading call has been sitting in
    /// <see cref="CallStatus.calling"/> for longer than <see cref="StaleAfter"/>, which is
    /// comfortably beyond the worst-case run (four sequential LLM calls, each with its own
    /// 180-second total timeout, plus content-failure re-prompts). A grading still legitimately
    /// in flight is never touched.</para>
    /// </summary>
    public class StrandedGradingReclaimJob
    {
        /// <summary>
        /// Well clear of a real run: the four stages have measured at ~5 minutes end to end, and
        /// each LLM call is separately bounded by Polly's 180-second total timeout.
        /// </summary>
        public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);

        private readonly AppDbContext _context;
        private readonly ILogger<StrandedGradingReclaimJob> _logger;

        public StrandedGradingReclaimJob(AppDbContext context, ILogger<StrandedGradingReclaimJob> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task RunAsync(CancellationToken cancellationToken = default)
        {
            var cutoff = DateTimeOffset.UtcNow - StaleAfter;

            var stale = await _context.AiCallLogs
                .Where(log => log.RequestType == AiOperationType.grading
                    && log.Status == CallStatus.calling
                    && log.CreatedAt < cutoff
                    && log.RelatedId != null)
                .ToListAsync(cancellationToken);

            if (stale.Count == 0)
            {
                return;
            }

            var submissionIds = stale.Select(log => log.RelatedId!.Value).ToHashSet();
            var submissions = await _context.Submissions
                .Where(s => submissionIds.Contains(s.Id) && s.Status == SubmissionStatus.grading)
                .ToListAsync(cancellationToken);

            foreach (var log in stale)
            {
                log.Status = CallStatus.final_failure;
                log.LastErrorMessage =
                    $"Abandoned: no result after {StaleAfter.TotalMinutes:0} minutes. The process most likely stopped mid-run; reclaimed so the submission can be graded again.";
                log.ResolvedAt = DateTimeOffset.UtcNow;
            }

            foreach (var submission in submissions)
            {
                submission.TransitionTo(SubmissionStatus.grading_failed);
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Reclaimed {SubmissionCount} submission(s) stranded in Grading and closed {LogCount} abandoned grading call log(s).",
                submissions.Count,
                stale.Count);
        }
    }
}
