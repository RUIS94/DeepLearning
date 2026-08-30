using DeepLearning.Application.Common;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Events;
using MediatR;

namespace DeepLearning.Application.Features.WeakPoints.EventHandlers
{
    /// <summary>
    /// Design doc §10.4/§10.5's weak-point tracking, done as deterministic rule-based bucketing
    /// rather than an extra AI call: the flowchart's "AI归类薄弱点" step assumes a classification
    /// pass, but ai_call_logs.request_type/prompt_templates.template_type have no enum value for
    /// it, and every signal it would need (error_category, dimension_key) already came out of
    /// the grading call that just ran — re-deriving the same categories with a second AI call
    /// would be pure duplicated cost. Category = "{DimensionName} - {ErrorCategoryName}", the
    /// most specific grouping the existing structured grading output actually supports.
    /// </summary>
    public class UpdateWeakPointsOnGraded : INotificationHandler<DomainEventNotification<SubmissionGradedEvent>>
    {
        private readonly ISubmissionRepository _submissionRepository;
        private readonly IWeakPointRepository _weakPointRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateWeakPointsOnGraded(
            ISubmissionRepository submissionRepository,
            IWeakPointRepository weakPointRepository,
            IUnitOfWork unitOfWork)
        {
            _submissionRepository = submissionRepository;
            _weakPointRepository = weakPointRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(DomainEventNotification<SubmissionGradedEvent> notification, CancellationToken cancellationToken)
        {
            var gradedEvent = notification.DomainEvent;
            var errors = await _submissionRepository.GetErrorListAsync(gradedEvent.SubmissionId, cancellationToken);
            if (errors.Count == 0)
            {
                return;
            }

            // Dedup to one weak point per distinct (dimension, category) pair per submission —
            // the same category flagged 3 times in one submission is one occurrence of that weak
            // point, not three. Recurrence (§10.4) is about a weak point resurfacing ACROSS
            // submissions after being resolved, not about density within a single submission.
            var categories = errors
                .Select(e => $"{e.Dimension!.DimensionName} - {e.ErrorTaxonomy!.CategoryName}")
                .Distinct()
                .ToList();

            var now = DateTimeOffset.UtcNow;
            foreach (var category in categories)
            {
                var weakPoint = await _weakPointRepository.GetByUserAndCategoryAsync(gradedEvent.UserId, category, cancellationToken);
                var isRecurrence = false;

                if (weakPoint is null)
                {
                    weakPoint = new WeakPoint
                    {
                        Id = Guid.NewGuid(),
                        UserId = gradedEvent.UserId,
                        Category = category,
                        Description = $"Recurring issues in '{category}'.",
                        FirstDetectedAt = now,
                        LastSeenAt = now,
                        RecurrenceCount = 0,
                        Status = WeakPointStatus.active,
                        Priority = Priority.medium,
                    };
                    await _weakPointRepository.AddAsync(weakPoint, cancellationToken);
                }
                else
                {
                    isRecurrence = weakPoint.Status == WeakPointStatus.resolved;

                    weakPoint.LastSeenAt = now;
                    weakPoint.Status = WeakPointStatus.active;

                    if (isRecurrence)
                    {
                        weakPoint.RecurrenceCount += 1;
                        weakPoint.Priority = Priority.high;
                        weakPoint.AddDomainEvent(new WeakPointRecurredEvent
                        {
                            WeakPointId = weakPoint.Id,
                            UserId = weakPoint.UserId,
                            Category = weakPoint.Category,
                            RecurrenceCount = weakPoint.RecurrenceCount,
                            RecurredAt = now,
                        });
                    }
                }

                await _weakPointRepository.AddOccurrenceAsync(new WeakPointOccurrence
                {
                    Id = Guid.NewGuid(),
                    WeakPointId = weakPoint.Id,
                    SubmissionId = gradedEvent.SubmissionId,
                    IsRecurrence = isRecurrence,
                    CreatedAt = now,
                }, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
