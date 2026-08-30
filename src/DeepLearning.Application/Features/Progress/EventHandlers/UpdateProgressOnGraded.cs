using DeepLearning.Application.Common;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Events;
using MediatR;

namespace DeepLearning.Application.Features.Progress.EventHandlers
{
    /// <summary>
    /// Incrementally upserts TODAY's progress_snapshots row (UserId + Difficulty tier) by
    /// recomputing its averages/pass-rate from source grading_results, rather than doing true
    /// incremental arithmetic on the existing row — recomputing from source avoids the class of
    /// bug where an incremental average silently drifts from what a fresh query would show.
    /// This is deliberately the full extent of Step 6's progress handling: TrendNote/
    /// KeyTurningPoint (AI-driven trend interpretation) and historical backfill across past
    /// periods are Step 9's Hangfire job — this handler only ever touches "today"'s row.
    /// </summary>
    public class UpdateProgressOnGraded : INotificationHandler<DomainEventNotification<SubmissionGradedEvent>>
    {
        private readonly IQuestionRepository _questionRepository;
        private readonly IProgressRepository _progressRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateProgressOnGraded(
            IQuestionRepository questionRepository,
            IProgressRepository progressRepository,
            IUnitOfWork unitOfWork)
        {
            _questionRepository = questionRepository;
            _progressRepository = progressRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(DomainEventNotification<SubmissionGradedEvent> notification, CancellationToken cancellationToken)
        {
            var gradedEvent = notification.DomainEvent;

            var question = await _questionRepository.GetByIdAsync(gradedEvent.QuestionId, cancellationToken);
            if (question is null)
            {
                return;
            }

            var difficultyTier = question.Difficulty.ToString();
            var today = DateOnly.FromDateTime(gradedEvent.GradedAt.UtcDateTime);

            var results = await _progressRepository.GetGradingResultsForUserInPeriodAsync(
                gradedEvent.UserId, difficultyTier, today, today, cancellationToken);
            if (results.Count == 0)
            {
                return;
            }

            var submissionGroups = results.GroupBy(x => x.SubmissionId).ToList();
            var passCount = submissionGroups.Count(g => g.All(r => r.PassBool));
            var passRate = Math.Round(100m * passCount / submissionGroups.Count, 2);

            var snapshot = await _progressRepository.GetByUserPeriodAsync(gradedEvent.UserId, today, today, difficultyTier, cancellationToken);
            if (snapshot is null)
            {
                snapshot = new ProgressSnapshot
                {
                    Id = Guid.NewGuid(),
                    UserId = gradedEvent.UserId,
                    PeriodStart = today,
                    PeriodEnd = today,
                    DifficultyTier = difficultyTier,
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                await _progressRepository.AddAsync(snapshot, cancellationToken);
            }

            snapshot.AvgBandMeaningTransfer = AverageBand(results, "meaning_transfer");
            snapshot.AvgBandTextualNorms = AverageBand(results, "textual_norms");
            snapshot.AvgBandLanguageProficiency = AverageBand(results, "language_proficiency");
            snapshot.PassRate = passRate;

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // dimension_key values are the fixed set seeded for NAATI CT (design doc §6.5) — matches
        // the same convention as other hardcoded rule/policy keys elsewhere in this codebase
        // (e.g. GenerationPolicyRepository's "difficulty_distribution").
        private static decimal? AverageBand(List<GradingResult> results, string dimensionKey)
        {
            var bands = results
                .Where(r => r.Dimension!.DimensionKey == dimensionKey)
                .Select(r => (decimal)r.Band)
                .ToList();

            return bands.Count > 0 ? Math.Round(bands.Average(), 1) : null;
        }
    }
}
