using System.Text.Json;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using MediatR;

namespace DeepLearning.Application.Features.Progress.Commands.GenerateProgressTrendSnapshot
{
    /// <summary>
    /// Step 9 (design doc §11.2): recomputes one (user, difficulty tier, week) progress_snapshots
    /// row from source grading_results (ProgressSnapshotCalculator — the same pure logic Step 6's
    /// UpdateProgressOnGraded uses for "today"), then makes one AI call to narrate a trend/flag a
    /// key turning point against the trailing weeks of history.
    ///
    /// Deliberately split into two independently-persisted phases, unlike every other
    /// AI-orchestration handler in this codebase (GradeSubmissionCommandHandler et al., which all
    /// fail the whole operation if the AI step fails): the numeric aggregate is a pure recompute
    /// from data already in the DB and always succeeds; the AI narrative is best-effort
    /// commentary on top of it. This runs unattended out of a weekly batch job across every active
    /// user — one user's AI hiccup must not roll back numbers that were already correct, and there
    /// is no synchronous caller waiting on an all-or-nothing response to reject.
    ///
    /// Also idempotent on the AI step specifically: ProgressSnapshotJob re-sends every trailing
    /// week on every run (so the same call path covers both "this week" and "backfill"), so this
    /// handler skips the AI call entirely when a week already has a trend note and its recomputed
    /// aggregate hasn't changed since — otherwise every historical week would be re-narrated (and
    /// re-billed) on every single weekly run forever.
    /// </summary>
    public class GenerateProgressTrendSnapshotCommandHandler
        : IRequestHandler<GenerateProgressTrendSnapshotCommand, GenerateProgressTrendSnapshotResult>
    {
        private const int TrendHistoryWeeks = 4;
        private static readonly JsonSerializerOptions PayloadJsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly IExamTypeRepository _examTypeRepository;
        private readonly IProgressRepository _progressRepository;
        private readonly IAiCallLogRepository _aiCallLogRepository;
        private readonly IExamConfigLoader _examConfigLoader;
        private readonly ILlmClientResolver _llmClientResolver;
        private readonly IAiCallRetryExecutor _aiCallRetryExecutor;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<GenerateProgressTrendSnapshotCommandHandler> _logger;

        public GenerateProgressTrendSnapshotCommandHandler(
            IExamTypeRepository examTypeRepository,
            IProgressRepository progressRepository,
            IAiCallLogRepository aiCallLogRepository,
            IExamConfigLoader examConfigLoader,
            ILlmClientResolver llmClientResolver,
            IAiCallRetryExecutor aiCallRetryExecutor,
            IUnitOfWork unitOfWork,
            ILogger<GenerateProgressTrendSnapshotCommandHandler> logger)
        {
            _examTypeRepository = examTypeRepository;
            _progressRepository = progressRepository;
            _aiCallLogRepository = aiCallLogRepository;
            _examConfigLoader = examConfigLoader;
            _llmClientResolver = llmClientResolver;
            _aiCallRetryExecutor = aiCallRetryExecutor;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<GenerateProgressTrendSnapshotResult> Handle(
            GenerateProgressTrendSnapshotCommand request, CancellationToken cancellationToken)
        {
            _ = await _examTypeRepository.GetByIdAsync(request.ExamTypeId, cancellationToken)
                ?? throw new NotFoundException(nameof(ExamType), request.ExamTypeId);

            var results = await _progressRepository.GetGradingResultsForUserInPeriodAsync(
                request.UserId, request.DifficultyTier, request.PeriodStart, request.PeriodEnd, cancellationToken);
            if (results.Count == 0)
            {
                // No grading activity in this week for this tier — nothing to recompute and
                // nothing meaningful to narrate. Deliberately not persisting an all-null row.
                return new GenerateProgressTrendSnapshotResult(SnapshotId: null, Skipped: true, TrendNoteGenerated: false);
            }

            var aggregate = ProgressSnapshotCalculator.Compute(results);

            var existingSnapshot = await _progressRepository.GetByUserPeriodAsync(
                request.UserId, request.PeriodStart, request.PeriodEnd, request.DifficultyTier, cancellationToken);

            // ProgressSnapshotJob re-sends every one of its trailing 12 weeks on every weekly run
            // (that's what lets the same call double as both "this week's fresh snapshot" and
            // "backfill any week the job missed") — so without this check, a week that was fully
            // narrated in a prior run would get re-narrated by a fresh, paid AI call every single
            // week for no reason, for as long as the job keeps running. Skip the AI step (but not
            // the recompute above, which is free) whenever nothing about this week's numbers
            // actually changed since it was last narrated.
            var aggregateUnchanged = existingSnapshot is not null
                && existingSnapshot.AvgBandMeaningTransfer == aggregate.AvgBandMeaningTransfer
                && existingSnapshot.AvgBandTextualNorms == aggregate.AvgBandTextualNorms
                && existingSnapshot.AvgBandLanguageProficiency == aggregate.AvgBandLanguageProficiency
                && existingSnapshot.PassRate == aggregate.PassRate;
            var alreadyNarrated = existingSnapshot is not null && !string.IsNullOrWhiteSpace(existingSnapshot.TrendNote);

            var snapshot = existingSnapshot;
            if (snapshot is null)
            {
                snapshot = new ProgressSnapshot
                {
                    Id = Guid.NewGuid(),
                    UserId = request.UserId,
                    PeriodStart = request.PeriodStart,
                    PeriodEnd = request.PeriodEnd,
                    DifficultyTier = request.DifficultyTier,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                await _progressRepository.AddAsync(snapshot, cancellationToken);
            }

            snapshot.AvgBandMeaningTransfer = aggregate.AvgBandMeaningTransfer;
            snapshot.AvgBandTextualNorms = aggregate.AvgBandTextualNorms;
            snapshot.AvgBandLanguageProficiency = aggregate.AvgBandLanguageProficiency;
            snapshot.PassRate = aggregate.PassRate;

            // Persisted up front, independent of whether the AI narrative step below succeeds —
            // see the class doc comment for why this handler splits the two, unlike its siblings.
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            if (aggregateUnchanged && alreadyNarrated)
            {
                return new GenerateProgressTrendSnapshotResult(snapshot.Id, Skipped: false, TrendNoteGenerated: true);
            }

            var history = await _progressRepository.ListRecentBeforeAsync(
                request.UserId, request.DifficultyTier, request.PeriodStart, TrendHistoryWeeks, cancellationToken);

            var trendNoteGenerated = await TryGenerateTrendNoteAsync(request, snapshot, history, cancellationToken);

            return new GenerateProgressTrendSnapshotResult(snapshot.Id, Skipped: false, TrendNoteGenerated: trendNoteGenerated);
        }

        private async Task<bool> TryGenerateTrendNoteAsync(
            GenerateProgressTrendSnapshotCommand request,
            ProgressSnapshot snapshot,
            List<ProgressSnapshot> history,
            CancellationToken cancellationToken)
        {
            var aiCallLog = new AiCallLog
            {
                Id = Guid.NewGuid(),
                RequestType = AiOperationType.progress_trend,
                RelatedId = request.UserId,
                Status = CallStatus.calling,
                AttemptCount = 1,
                MaxRetries = 3,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await _aiCallLogRepository.AddAsync(aiCallLog, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var templateModel = new
            {
                DifficultyTier = request.DifficultyTier,
                PeriodStart = request.PeriodStart.ToString("yyyy-MM-dd"),
                PeriodEnd = request.PeriodEnd.ToString("yyyy-MM-dd"),
                Current = ToTemplateWeek(snapshot),
                // Oldest first, matching the order a human would read a trend line left-to-right —
                // history is fetched most-recent-first (the shape the "last N" query needs), so
                // reverse it here rather than complicating the repository query's own ordering.
                History = history.AsEnumerable().Reverse().Select(ToTemplateWeek).ToList(),
            };
            var prompt = await _examConfigLoader.BuildPromptAsync(
                request.ExamTypeId, AiOperationType.progress_trend, templateModel, cancellationToken);

            TrendPayload payload;
            try
            {
                payload = await _aiCallRetryExecutor.ExecuteAsync(aiCallLog, async () =>
                {
                    var llmClient = await _llmClientResolver.GetActiveClientAsync(cancellationToken);
                    var completion = await llmClient.CompleteAsync(
                        new LlmCompletionRequest(SystemPrompt: null, UserPrompt: prompt, MaxTokens: 1024),
                        cancellationToken);
                    aiCallLog.LatencyMs = completion.LatencyMs;

                    var parsed = ParsePayload(completion.Text);
                    ValidatePayload(parsed);
                    return parsed;
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                // Deliberately swallowed, not rethrown — see the class doc comment. The numeric
                // snapshot already committed successfully; only the narrative is lost this run,
                // and the next weekly run will retry it against the (by-then-updated) history.
                _logger.LogWarning(ex,
                    "Progress trend narrative failed for user {UserId}, tier {DifficultyTier}, week {PeriodStart}: {Message}",
                    request.UserId, request.DifficultyTier, request.PeriodStart, ex.Message);
                await FailAsync(aiCallLog, $"Failed after {aiCallLog.AttemptCount} attempt(s): {ex.Message}", cancellationToken);
                return false;
            }

            snapshot.TrendNote = payload.TrendNote;
            snapshot.KeyTurningPoint = payload.KeyTurningPoint;

            aiCallLog.Status = CallStatus.success;
            aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return true;
        }

        private async Task FailAsync(AiCallLog aiCallLog, string errorMessage, CancellationToken cancellationToken)
        {
            aiCallLog.Status = CallStatus.final_failure;
            aiCallLog.LastErrorMessage = errorMessage;
            aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private static object ToTemplateWeek(ProgressSnapshot s) => new
        {
            PeriodStart = s.PeriodStart.ToString("yyyy-MM-dd"),
            PeriodEnd = s.PeriodEnd.ToString("yyyy-MM-dd"),
            s.AvgBandMeaningTransfer,
            s.AvgBandTextualNorms,
            s.AvgBandLanguageProficiency,
            s.PassRate,
        };

        private static TrendPayload ParsePayload(string rawText)
        {
            var json = StripMarkdownFence(rawText.Trim());
            return JsonSerializer.Deserialize<TrendPayload>(json, PayloadJsonOptions)
                ?? throw new InvalidOperationException("Deserialized to null.");
        }

        // Same "structured output is a hard constraint" philosophy as every other AI-orchestration
        // handler in this codebase (design doc §10.3).
        private static void ValidatePayload(TrendPayload payload)
        {
            if (string.IsNullOrWhiteSpace(payload.TrendNote))
            {
                throw new InvalidOperationException("trendNote must not be empty.");
            }
        }

        private static string StripMarkdownFence(string text)
        {
            if (!text.StartsWith("```", StringComparison.Ordinal))
            {
                return text;
            }

            var firstNewLine = text.IndexOf('\n');
            var withoutOpeningFence = firstNewLine >= 0 ? text[(firstNewLine + 1)..] : text;
            var closingFenceIndex = withoutOpeningFence.LastIndexOf("```", StringComparison.Ordinal);
            return closingFenceIndex >= 0 ? withoutOpeningFence[..closingFenceIndex] : withoutOpeningFence;
        }

        private class TrendPayload
        {
            public string TrendNote { get; set; } = string.Empty;

            public bool KeyTurningPoint { get; set; }
        }
    }
}
