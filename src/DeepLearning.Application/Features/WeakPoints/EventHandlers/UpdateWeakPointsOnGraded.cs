using DeepLearning.Application.Common;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Events;
using MediatR;

namespace DeepLearning.Application.Features.WeakPoints.EventHandlers
{
    /// <summary>
    /// Design doc §10.4/§10.5's weak-point tracking. Each graded error is matched to a curated
    /// <see cref="WeakPointCatalog"/> row and the resulting <see cref="WeakPointOccurrence"/> is
    /// tied back to the specific ErrorListItem, its snippet and its dimension's band.
    ///
    /// Bucketing order per error: (1) an <see cref="IWeakPointClassifier"/> AI call, when a
    /// <c>weak_point_classification</c> template is configured — the only way to tell apart
    /// several distinct weak-point kinds that share one (dimension, error category) pair; then
    /// (2) the deterministic (DefaultDimensionKey [+ DefaultErrorCategory]) rule, most specific
    /// match winning; then (3) the legacy free-text "{DimensionName} - {ErrorCategoryName}"
    /// bucket (CatalogId null). The classifier never throws and returns nothing when it is off
    /// or fails, so with no template configured this is byte-for-byte the pre-classifier
    /// rule-only behaviour.
    /// </summary>
    public class UpdateWeakPointsOnGraded : INotificationHandler<DomainEventNotification<SubmissionGradedEvent>>
    {
        /// <summary>
        /// An active weak point not seen again within the user's most recent this-many graded
        /// submissions is marked resolved; a later occurrence then counts as a recurrence
        /// (design doc §10.4 — "学会了又忘了" is a distinct signal from "从未真正学会").
        /// </summary>
        private const int ResolveAfterUnseenSubmissions = 5;

        private readonly ISubmissionRepository _submissionRepository;
        private readonly IWeakPointRepository _weakPointRepository;
        private readonly IWeakPointCatalogRepository _weakPointCatalogRepository;
        private readonly IWeakPointClassifier _weakPointClassifier;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateWeakPointsOnGraded(
            ISubmissionRepository submissionRepository,
            IWeakPointRepository weakPointRepository,
            IWeakPointCatalogRepository weakPointCatalogRepository,
            IWeakPointClassifier weakPointClassifier,
            IUnitOfWork unitOfWork)
        {
            _submissionRepository = submissionRepository;
            _weakPointRepository = weakPointRepository;
            _weakPointCatalogRepository = weakPointCatalogRepository;
            _weakPointClassifier = weakPointClassifier;
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

            var catalog = await _weakPointCatalogRepository.ListByExamTypeAsync(gradedEvent.ExamTypeId, cancellationToken);
            var gradingResults = await _submissionRepository.GetGradingResultsAsync(gradedEvent.SubmissionId, cancellationToken);
            var bandByDimensionId = gradingResults
                .GroupBy(r => r.DimensionId)
                .ToDictionary(g => g.Key, g => g.First().Band);

            // One AI pass to place errors the (dimension, category) rule can't tell apart.
            // Returns nothing (-> rule handles everything) when no template is configured or the
            // call fails; never throws. See IWeakPointClassifier.
            var classifierErrors = errors.Select(e => new WeakPointClassifierError(
                e.Id,
                e.Dimension?.DimensionKey ?? string.Empty,
                e.ErrorTaxonomy?.CategoryKey ?? string.Empty,
                e.UserTextSnippet ?? e.SourceTextSnippet,
                e.Explanation,
                e.ImpactsCore)).ToList();
            var aiCatalogIdByErrorId = await _weakPointClassifier.ClassifyAsync(
                gradedEvent.ExamTypeId, classifierErrors, catalog, cancellationToken);
            var catalogById = catalog.ToDictionary(c => c.Id);

            // Dedup to one bucket per submission — the same category flagged 3 times in one
            // submission is one occurrence of that weak point, not three. Recurrence (§10.4) is
            // about a weak point resurfacing ACROSS submissions after being resolved, not density
            // within one. First error seen for a bucket becomes its representative for the
            // occurrence's snippet / band / error_list_id.
            var buckets = new Dictionary<string, Bucket>();
            foreach (var error in errors)
            {
                var bucket = ResolveBucket(error, catalog, aiCatalogIdByErrorId, catalogById);
                if (!buckets.ContainsKey(bucket.Key))
                {
                    bucket.RepresentativeError = error;
                    buckets[bucket.Key] = bucket;
                }
            }

            var now = DateTimeOffset.UtcNow;
            var touchedWeakPointIds = new HashSet<Guid>();
            foreach (var bucket in buckets.Values)
            {
                var representative = bucket.RepresentativeError!;
                var evidence = BuildEvidenceNote(representative);

                var weakPoint = bucket.CatalogId is { } catalogId
                    ? await _weakPointRepository.GetByUserAndCatalogAsync(gradedEvent.UserId, catalogId, cancellationToken)
                    : await _weakPointRepository.GetByUserAndCategoryAsync(gradedEvent.UserId, bucket.Category, cancellationToken);

                var isRecurrence = false;

                if (weakPoint is null)
                {
                    weakPoint = new WeakPoint
                    {
                        Id = Guid.NewGuid(),
                        UserId = gradedEvent.UserId,
                        ExamTypeId = gradedEvent.ExamTypeId,
                        CatalogId = bucket.CatalogId,
                        Category = bucket.Category,
                        Description = bucket.Description,
                        DetectionSource = bucket.Source,
                        FirstDetectedAt = now,
                        LastSeenAt = now,
                        RecurrenceCount = 0,
                        Status = WeakPointStatus.active,
                        Priority = Priority.medium,
                        EvidenceNote = evidence,
                    };
                    await _weakPointRepository.AddAsync(weakPoint, cancellationToken);
                }
                else
                {
                    isRecurrence = weakPoint.Status == WeakPointStatus.resolved;

                    weakPoint.LastSeenAt = now;
                    weakPoint.Status = WeakPointStatus.active;
                    weakPoint.ResolvedAt = null;
                    weakPoint.EvidenceNote = evidence;
                    weakPoint.ExamTypeId ??= gradedEvent.ExamTypeId;

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

                bandByDimensionId.TryGetValue(representative.DimensionId, out var band);
                touchedWeakPointIds.Add(weakPoint.Id);

                await _weakPointRepository.AddOccurrenceAsync(new WeakPointOccurrence
                {
                    Id = Guid.NewGuid(),
                    WeakPointId = weakPoint.Id,
                    SubmissionId = gradedEvent.SubmissionId,
                    ErrorListId = representative.Id == Guid.Empty ? null : representative.Id,
                    Snippet = representative.UserTextSnippet ?? representative.SourceTextSnippet,
                    DetectedBand = band == 0 ? null : band,
                    IsRecurrence = isRecurrence,
                    CreatedAt = now,
                }, cancellationToken);
            }

            await ResolveStaleWeakPointsAsync(gradedEvent.UserId, touchedWeakPointIds, now, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Marks the user's active weak points that did NOT resurface in this submission and
        /// haven't been seen within their most recent <see cref="ResolveAfterUnseenSubmissions"/>
        /// graded submissions as resolved. Nothing to do until the user has that many graded
        /// submissions on record.
        /// </summary>
        private async Task ResolveStaleWeakPointsAsync(
            Guid userId, HashSet<Guid> touchedWeakPointIds, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var activeWeakPoints = await _weakPointRepository.ListByUserAsync(userId, WeakPointStatus.active, cancellationToken);
            var stillActiveUntouched = activeWeakPoints.Where(w => !touchedWeakPointIds.Contains(w.Id)).ToList();
            if (stillActiveUntouched.Count == 0)
            {
                return;
            }

            var gradedCreatedAt = (await _submissionRepository.ListByUserAsync(userId, null, cancellationToken))
                .Where(s => s.Status == SubmissionStatus.graded)
                .Select(s => s.CreatedAt)
                .OrderByDescending(x => x)
                .ToList();
            if (gradedCreatedAt.Count < ResolveAfterUnseenSubmissions)
            {
                return;
            }

            var cutoff = gradedCreatedAt[ResolveAfterUnseenSubmissions - 1];
            foreach (var weakPoint in stillActiveUntouched.Where(w => w.LastSeenAt < cutoff))
            {
                weakPoint.Status = WeakPointStatus.resolved;
                weakPoint.ResolvedAt = now;
            }
        }

        /// <summary>
        /// AI classification first (when it confidently placed this error), then the most
        /// specific deterministic catalog match (both dimension + error category beats dimension
        /// alone), then the legacy free-text bucket.
        /// </summary>
        private static Bucket ResolveBucket(
            ErrorListItem error,
            IReadOnlyCollection<WeakPointCatalog> catalog,
            IReadOnlyDictionary<Guid, Guid> aiCatalogIdByErrorId,
            IReadOnlyDictionary<Guid, WeakPointCatalog> catalogById)
        {
            if (aiCatalogIdByErrorId.TryGetValue(error.Id, out var aiCatalogId)
                && catalogById.TryGetValue(aiCatalogId, out var aiMatch))
            {
                return CatalogBucket(aiMatch, "ai");
            }

            var dimensionKey = error.Dimension?.DimensionKey;
            var categoryKey = error.ErrorTaxonomy?.CategoryKey;

            WeakPointCatalog? match = null;
            if (!string.IsNullOrEmpty(dimensionKey))
            {
                match =
                    catalog.FirstOrDefault(c =>
                        c.DefaultDimensionKey == dimensionKey && c.DefaultErrorCategory == categoryKey)
                    ?? catalog.FirstOrDefault(c =>
                        c.DefaultDimensionKey == dimensionKey && c.DefaultErrorCategory == null);
            }

            if (match is not null)
            {
                return CatalogBucket(match, "rule");
            }

            var dimensionName = error.Dimension?.DimensionName ?? "Unknown dimension";
            var categoryName = error.ErrorTaxonomy?.CategoryName ?? "Unknown category";
            var legacy = $"{dimensionName} - {categoryName}";
            return new Bucket
            {
                Key = $"legacy:{legacy}",
                CatalogId = null,
                Category = legacy,
                Description = $"Recurring issues in '{legacy}'.",
                Source = "rule",
            };
        }

        private static Bucket CatalogBucket(WeakPointCatalog match, string source) => new()
        {
            Key = $"catalog:{match.Id}",
            CatalogId = match.Id,
            Category = match.Code,
            Description = match.Description,
            Source = source,
        };

        private static string BuildEvidenceNote(ErrorListItem error)
        {
            var snippet = error.UserTextSnippet ?? error.SourceTextSnippet;
            var where = string.IsNullOrWhiteSpace(error.PositionRef) ? null : $"[{error.PositionRef}] ";
            var explanation = string.IsNullOrWhiteSpace(error.Explanation) ? null : error.Explanation!.Trim();
            var text = $"{where}{snippet}{(snippet is not null && explanation is not null ? " — " : null)}{explanation}".Trim();
            return text.Length > 400 ? text[..400] : text;
        }

        private sealed class Bucket
        {
            public required string Key { get; init; }
            public Guid? CatalogId { get; init; }
            public required string Category { get; init; }
            public required string Description { get; init; }

            /// <summary><c>ai</c> when an IWeakPointClassifier assignment produced this bucket, otherwise <c>rule</c>.</summary>
            public required string Source { get; init; }
            public ErrorListItem? RepresentativeError { get; set; }
        }
    }
}
