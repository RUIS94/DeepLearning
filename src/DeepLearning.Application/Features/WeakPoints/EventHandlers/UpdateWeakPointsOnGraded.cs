using System.Text;
using DeepLearning.Application.Common;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Events;
using MediatR;

namespace DeepLearning.Application.Features.WeakPoints.EventHandlers
{
    /// <summary>
    /// Design doc §10.4/§10.5's weak-point tracking. Each graded error is matched to a
    /// <see cref="WeakPointCatalog"/> kind and the resulting <see cref="WeakPointOccurrence"/> is
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
    ///
    /// The same classifier call also returns a refreshed per-learner <c>pattern_summary</c> for
    /// each kind this submission touched (merged from the prior summary + new evidence); that is
    /// the text injected into the grading prompt. A legacy bucket that recurs (already existed
    /// before this grading and gets another hit) is promoted: a <c>proposed</c> catalog kind is
    /// minted for it and the weak point re-pointed, so it stops being a coarse free-text row and
    /// becomes reviewable/mergeable in admin.
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

            // The user's full active set (not the grading-prompt top-K) — the classifier needs
            // every catalog-mapped summary so it can merge rather than rewrite.
            var activeWeakPoints = await _weakPointRepository.ListActiveWithCatalogByUserAsync(
                gradedEvent.UserId, limit: null, cancellationToken);
            var activeSummaries = activeWeakPoints
                .Where(w => w.Catalog is not null)
                .Select(w => new ActiveWeakPointSummary(w.Catalog!.Code, w.PatternSummary))
                .ToList();

            // One AI pass: place errors the (dimension, category) rule can't tell apart, and
            // refresh pattern summaries. Returns Empty (-> rule handles everything, no summary
            // updates) when no template is configured or the call fails; never throws.
            var classifierErrors = errors.Select(e => new WeakPointClassifierError(
                e.Id,
                e.Dimension?.DimensionKey ?? string.Empty,
                e.ErrorTaxonomy?.CategoryKey ?? string.Empty,
                e.UserTextSnippet ?? e.SourceTextSnippet,
                e.Explanation,
                e.Severity)).ToList();
            var classification = await _weakPointClassifier.ClassifyAsync(
                gradedEvent.ExamTypeId, classifierErrors, catalog, activeSummaries, cancellationToken);

            var catalogById = catalog.ToDictionary(c => c.Id);
            var summaryByCode = classification.CatalogCodeToPatternSummary;

            // Dedup to one bucket per submission — the same category flagged 3 times in one
            // submission is one occurrence of that weak point, not three. Recurrence (§10.4) is
            // about a weak point resurfacing ACROSS submissions after being resolved, not density
            // within one. First error seen for a bucket becomes its representative.
            var buckets = new Dictionary<string, Bucket>();
            foreach (var error in errors)
            {
                var bucket = ResolveBucket(error, catalog, classification.ErrorToCatalogId, catalogById);
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

                var weakPoint = bucket.CatalogId is { } catalogId
                    ? await _weakPointRepository.GetByUserAndCatalogAsync(gradedEvent.UserId, catalogId, cancellationToken)
                    : await _weakPointRepository.GetByUserAndCategoryAsync(gradedEvent.UserId, bucket.Category, cancellationToken);

                var wasExisting = weakPoint is not null;
                var isRecurrence = false;

                var aiSummary = bucket.CatalogId is { } cid
                    && catalogById.TryGetValue(cid, out var catRow)
                    && summaryByCode.TryGetValue(catRow.Code, out var s)
                        ? s
                        : null;

                if (weakPoint is null)
                {
                    weakPoint = new WeakPoint
                    {
                        Id = Guid.NewGuid(),
                        UserId = gradedEvent.UserId,
                        ExamTypeId = gradedEvent.ExamTypeId,
                        CatalogId = bucket.CatalogId,
                        Category = bucket.CatalogId is null ? bucket.Category : null,
                        PatternSummary = aiSummary ?? (bucket.CatalogId is null ? BuildLegacySummary(representative, 1) : null),
                        DetectionSource = bucket.Source,
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
                    weakPoint.ResolvedAt = null;
                    weakPoint.ExamTypeId ??= gradedEvent.ExamTypeId;

                    if (isRecurrence)
                    {
                        weakPoint.RecurrenceCount += 1;
                        weakPoint.Priority = Priority.high;
                        weakPoint.AddDomainEvent(new WeakPointRecurredEvent
                        {
                            WeakPointId = weakPoint.Id,
                            UserId = weakPoint.UserId,
                            Category = bucket.Category,
                            RecurrenceCount = weakPoint.RecurrenceCount,
                            RecurredAt = now,
                        });
                    }

                    if (aiSummary is not null)
                    {
                        weakPoint.PatternSummary = aiSummary;
                    }
                    else if (weakPoint.CatalogId is null)
                    {
                        weakPoint.PatternSummary = BuildLegacySummary(representative, weakPoint.RecurrenceCount + 2);
                    }

                    // A legacy bucket that already existed and hit again -> mint a proposed
                    // catalog kind for it and re-point, so it becomes reviewable/mergeable.
                    if (weakPoint.CatalogId is null)
                    {
                        var promoted = await PromoteLegacyBucketAsync(gradedEvent.ExamTypeId, weakPoint, representative, cancellationToken);
                        if (promoted is not null)
                        {
                            weakPoint.CatalogId = promoted.Id;
                            weakPoint.Category = null;
                            weakPoint.DetectionSource = "rule";
                        }
                    }
                }

                bandByDimensionId.TryGetValue(representative.DimensionId, out var band);
                touchedWeakPointIds.Add(weakPoint.Id);

                // Re-grade / concurrent-event guard: at most one occurrence per (weak point, submission).
                if (wasExisting
                    && await _weakPointRepository.OccurrenceExistsAsync(weakPoint.Id, gradedEvent.SubmissionId, cancellationToken))
                {
                    continue;
                }

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
        /// Mints a <see cref="WeakPointCatalogStatus.proposed"/> catalog kind for a recurring
        /// legacy bucket (deterministic code from the representative error's dimension + category,
        /// so two learners hitting the same pattern converge on one row). Returns the existing
        /// row if one already carries that code. Null only when the representative lacks a
        /// dimension/category to key on.
        /// </summary>
        private async Task<WeakPointCatalog?> PromoteLegacyBucketAsync(
            Guid examTypeId, WeakPoint weakPoint, ErrorListItem representative, CancellationToken cancellationToken)
        {
            var dimensionKey = representative.Dimension?.DimensionKey;
            var categoryKey = representative.ErrorTaxonomy?.CategoryKey;
            if (string.IsNullOrEmpty(dimensionKey) || string.IsNullOrEmpty(categoryKey))
            {
                return null;
            }

            var code = $"auto_{dimensionKey}_{categoryKey}";
            if (code.Length > 60)
            {
                code = code[..60];
            }

            var existing = await _weakPointRepository.GetCatalogByExamAndCodeAsync(examTypeId, code, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }

            var name = weakPoint.Category ?? $"{representative.Dimension?.DimensionName} - {representative.ErrorTaxonomy?.CategoryName}";
            var proposed = new WeakPointCatalog
            {
                Id = Guid.NewGuid(),
                ExamTypeId = examTypeId,
                Code = code,
                Name = name.Length > 100 ? name[..100] : name,
                Description = weakPoint.PatternSummary ?? $"待审:{name} 反复出现,尚未归入规范薄弱点。",
                DefaultDimensionKey = dimensionKey,
                DefaultErrorCategory = categoryKey,
                Status = WeakPointCatalogStatus.proposed,
                Origin = "auto",
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await _weakPointRepository.AddCatalogAsync(proposed, cancellationToken);
            return proposed;
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

            var gradedCreatedAt = await _submissionRepository.ListRecentGradedCreatedAtAsync(
                userId, ResolveAfterUnseenSubmissions, cancellationToken);
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
                Source = "rule",
            };
        }

        private static Bucket CatalogBucket(WeakPointCatalog match, string source) => new()
        {
            Key = $"catalog:{match.Id}",
            CatalogId = match.Id,
            Category = match.Code,
            Source = source,
        };

        /// <summary>Deterministic per-learner summary for a legacy (catalog-less) bucket — no AI call.</summary>
        private static string BuildLegacySummary(ErrorListItem representative, int occurrenceCount)
        {
            var where = string.IsNullOrWhiteSpace(representative.PositionRef) ? null : $"[{representative.PositionRef}] ";
            var snippet = representative.UserTextSnippet ?? representative.SourceTextSnippet;
            var dim = representative.Dimension?.DimensionName;
            var cat = representative.ErrorTaxonomy?.CategoryName;

            var sb = new StringBuilder();
            sb.Append(dim is not null && cat is not null ? $"{dim} / {cat}" : "反复出现的问题");
            sb.Append($":第 {occurrenceCount} 次");
            if (snippet is not null)
            {
                sb.Append($";最近 {where}「{snippet}」");
            }

            var text = sb.ToString();
            return text.Length > 240 ? text[..240] : text;
        }

        private sealed class Bucket
        {
            public required string Key { get; init; }
            public Guid? CatalogId { get; init; }

            /// <summary>Catalog code for a catalog bucket, or the legacy "{Dim} - {Cat}" label. Also the legacy dedup lookup key.</summary>
            public required string Category { get; init; }

            /// <summary><c>ai</c> when an IWeakPointClassifier assignment produced this bucket, otherwise <c>rule</c>.</summary>
            public required string Source { get; init; }
            public ErrorListItem? RepresentativeError { get; set; }
        }
    }
}
