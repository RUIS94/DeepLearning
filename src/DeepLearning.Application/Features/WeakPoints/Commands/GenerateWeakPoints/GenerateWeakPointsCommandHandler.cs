using System.Text;
using DeepLearning.Application.Common;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Events;
using MediatR;

namespace DeepLearning.Application.Features.WeakPoints.Commands.GenerateWeakPoints
{
    /// <summary>
    /// Design doc §10.4/§10.5's weak-point tracking, run as a background job after grading
    /// (see GenerateWeakPointsCommand for why it is no longer a SubmissionGradedEvent
    /// subscriber). Each graded error is matched to a <see cref="WeakPointCatalog"/> kind and the
    /// resulting <see cref="WeakPointOccurrence"/> is tied back to the specific ErrorListItem, its
    /// snippet and its dimension's band.
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
    ///
    /// 薄弱点分类与生命周期管理_策划书.md's three-stage lifecycle (catalog-mapped weak points only —
    /// legacy free-text buckets keep the pre-existing "detected once -&gt; immediately active"
    /// behaviour, since the tracking/threshold model only applies to the curated two-level
    /// taxonomy):
    ///   1. First hit -&gt; <see cref="WeakPointStatus.tracking"/>. Each subsequent hit (deduped to
    ///      one per submission, counted regardless of status) increments
    ///      <see cref="WeakPoint.OccurrenceSubmissionCount"/>; crossing 3 while tracking -&gt;
    ///      <see cref="WeakPointStatus.active"/> and queues an <see cref="IWeakPointDetectionCriteriaGenerator"/>
    ///      call. A hit on an already-<c>resolved</c> weak point reactivates it immediately
    ///      (bypassing the threshold — the existing recurrence path) and also queues a fresh
    ///      detection-criteria generation, since the recurrence itself is new evidence.
    ///   2. Once active, every submission that does NOT hit it is a candidate for
    ///      <see cref="IWeakPointRecheckService"/>: batched across all such candidates for this
    ///      submission, checked against this submission's source text + translation. `resolved`
    ///      deactivates it; `still_weak` keeps it active; `not_present` is inconclusive and only
    ///      deactivates after <see cref="WeakPoint.NoEvidenceStreak"/> reaches 5 (no evidence
    ///      either way for that long). None of this touches error_list, RecurrenceCount or
    ///      OccurrenceSubmissionCount — a recheck is a status judgment, not a new occurrence.
    /// </summary>
    public class GenerateWeakPointsCommandHandler : IRequestHandler<GenerateWeakPointsCommand>
    {
        /// <summary>Catalog-mapped weak points need this many distinct-submission hits while <see cref="WeakPointStatus.tracking"/> before they are confirmed <see cref="WeakPointStatus.active"/>.</summary>
        private const int TrackingConfirmationThreshold = 3;

        /// <summary>Consecutive inconclusive ("not_present") recheck results before an active weak point is deactivated for lack of any recent opportunity to confirm it either way.</summary>
        private const int NoEvidenceResolveThreshold = 5;

        private readonly ISubmissionRepository _submissionRepository;
        private readonly IWeakPointRepository _weakPointRepository;
        private readonly IWeakPointCatalogRepository _weakPointCatalogRepository;
        private readonly IWeakPointCategoryRepository _weakPointCategoryRepository;
        private readonly IWeakPointClassifier _weakPointClassifier;
        private readonly IWeakPointDetectionCriteriaGenerator _detectionCriteriaGenerator;
        private readonly IWeakPointRecheckService _recheckService;
        private readonly IUnitOfWork _unitOfWork;

        public GenerateWeakPointsCommandHandler(
            ISubmissionRepository submissionRepository,
            IWeakPointRepository weakPointRepository,
            IWeakPointCatalogRepository weakPointCatalogRepository,
            IWeakPointCategoryRepository weakPointCategoryRepository,
            IWeakPointClassifier weakPointClassifier,
            IWeakPointDetectionCriteriaGenerator detectionCriteriaGenerator,
            IWeakPointRecheckService recheckService,
            IUnitOfWork unitOfWork)
        {
            _submissionRepository = submissionRepository;
            _weakPointRepository = weakPointRepository;
            _weakPointCatalogRepository = weakPointCatalogRepository;
            _weakPointCategoryRepository = weakPointCategoryRepository;
            _weakPointClassifier = weakPointClassifier;
            _detectionCriteriaGenerator = detectionCriteriaGenerator;
            _recheckService = recheckService;
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(GenerateWeakPointsCommand request, CancellationToken cancellationToken)
        {
            var gradedEvent = request;
            var now = DateTimeOffset.UtcNow;
            var touchedWeakPointIds = new HashSet<Guid>();

            var errors = await _submissionRepository.GetErrorListAsync(gradedEvent.SubmissionId, cancellationToken);
            if (errors.Count > 0)
            {
                await ClassifyAndTrackAsync(gradedEvent, errors, now, touchedWeakPointIds, cancellationToken);
            }

            await RecheckUntouchedActiveWeakPointsAsync(gradedEvent, now, touchedWeakPointIds, cancellationToken);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private async Task ClassifyAndTrackAsync(
            GenerateWeakPointsCommand gradedEvent,
            List<ErrorListItem> errors,
            DateTimeOffset now,
            HashSet<Guid> touchedWeakPointIds,
            CancellationToken cancellationToken)
        {
            var catalog = await _weakPointCatalogRepository.ListActiveAsync(cancellationToken);
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

            var classifierErrors = errors.Select(e => new WeakPointClassifierError(
                e.Id,
                e.Dimension?.DimensionKey ?? string.Empty,
                e.ErrorTaxonomy?.CategoryKey ?? string.Empty,
                e.UserTextSnippet ?? e.SourceTextSnippet,
                e.Explanation,
                e.Severity)).ToList();
            var classification = await _weakPointClassifier.ClassifyAsync(
                gradedEvent.ExamTypeId, classifierErrors, catalog, activeSummaries, cancellationToken);

            await CreateProposedLeavesAsync(classification.ProposedLeaves, cancellationToken);

            var catalogById = catalog.ToDictionary(c => c.Id);
            var summaryByCode = classification.CatalogCodeToPatternSummary;

            // Dedup to one bucket per submission — the same category flagged 3 times in one
            // submission is one occurrence of that weak point, not three. Recurrence is about a
            // weak point resurfacing ACROSS submissions after being resolved, not density within one.
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

            var needsDetectionCriteria = new List<WeakPoint>();

            foreach (var bucket in buckets.Values)
            {
                var representative = bucket.RepresentativeError!;
                var isCatalogMapped = bucket.CatalogId is not null;

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
                        OccurrenceSubmissionCount = 1,
                        // Legacy (catalog-less) buckets keep the pre-existing immediate-active
                        // behaviour — the tracking/threshold model only applies to catalog-mapped
                        // weak points (策划书 §2).
                        Status = isCatalogMapped ? WeakPointStatus.tracking : WeakPointStatus.active,
                        Priority = Priority.medium,
                    };
                    await _weakPointRepository.AddAsync(weakPoint, cancellationToken);
                }
                else
                {
                    var previousStatus = weakPoint.Status;
                    isRecurrence = previousStatus == WeakPointStatus.resolved;

                    weakPoint.LastSeenAt = now;
                    weakPoint.ResolvedAt = null;
                    weakPoint.ExamTypeId ??= gradedEvent.ExamTypeId;
                    weakPoint.OccurrenceSubmissionCount += 1;

                    if (isRecurrence)
                    {
                        weakPoint.Status = WeakPointStatus.active;
                        weakPoint.RecurrenceCount += 1;
                        weakPoint.Priority = Priority.high;
                        weakPoint.NoEvidenceStreak = 0;
                        weakPoint.AddDomainEvent(new WeakPointRecurredEvent
                        {
                            WeakPointId = weakPoint.Id,
                            UserId = weakPoint.UserId,
                            Category = bucket.Category,
                            RecurrenceCount = weakPoint.RecurrenceCount,
                            RecurredAt = now,
                        });

                        // The recurrence itself is new evidence — regenerate rather than reuse
                        // whatever criteria was generated before this weak point was resolved.
                        if (isCatalogMapped)
                        {
                            needsDetectionCriteria.Add(weakPoint);
                        }
                    }
                    else if (previousStatus == WeakPointStatus.tracking)
                    {
                        if (weakPoint.OccurrenceSubmissionCount >= TrackingConfirmationThreshold)
                        {
                            weakPoint.Status = WeakPointStatus.active;
                            needsDetectionCriteria.Add(weakPoint);
                        }
                        // else: stays tracking, not yet confirmed.
                    }
                    else
                    {
                        // Already active and hit again — strong evidence, resets the "no recent
                        // opportunity to confirm" counter the recheck maintains.
                        weakPoint.NoEvidenceStreak = 0;
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
                        var promoted = await PromoteLegacyBucketAsync(weakPoint, representative, cancellationToken);
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

            if (needsDetectionCriteria.Count > 0)
            {
                await GenerateDetectionCriteriaAsync(gradedEvent.ExamTypeId, needsDetectionCriteria, catalogById, cancellationToken);
            }
        }

        /// <summary>
        /// Creates a <see cref="WeakPointCatalogStatus.proposed"/> row for each new-leaf suggestion
        /// the classifier judged necessary, skipping any whose code already exists (a concurrent
        /// grading run, or a duplicate suggestion across errors in the same response — the
        /// classifier already dedups the latter, this is the cross-request race guard).
        /// </summary>
        private async Task CreateProposedLeavesAsync(
            IReadOnlyList<ProposedCatalogLeaf> proposedLeaves, CancellationToken cancellationToken)
        {
            foreach (var proposal in proposedLeaves)
            {
                if (await _weakPointCatalogRepository.ExistsAsync(proposal.Code, cancellationToken))
                {
                    continue;
                }

                var category = await _weakPointCategoryRepository.GetByCodeAsync(proposal.CategoryCode, cancellationToken);
                await _weakPointCatalogRepository.AddAsync(new WeakPointCatalog
                {
                    Id = Guid.NewGuid(),
                    CategoryId = category?.Id,
                    Code = proposal.Code,
                    Name = proposal.Name,
                    Description = proposal.Description,
                    Status = WeakPointCatalogStatus.proposed,
                    Origin = "auto",
                    CreatedAt = DateTimeOffset.UtcNow,
                }, cancellationToken);
            }
        }

        /// <summary>
        /// One batched weak_point_detection_criteria call for every weak point that crossed the
        /// tracking threshold or reactivated this round (usually 0 or 1). A weak point the
        /// generator could not confidently produce criteria for keeps its current value (null for
        /// a first-time activation) rather than blocking the status transition already applied.
        /// </summary>
        private async Task GenerateDetectionCriteriaAsync(
            Guid examTypeId,
            List<WeakPoint> needsDetectionCriteria,
            Dictionary<Guid, WeakPointCatalog> catalogById,
            CancellationToken cancellationToken)
        {
            var distinct = needsDetectionCriteria.DistinctBy(w => w.Id).ToList();
            var requests = new List<WeakPointDetectionCriteriaRequest>();
            foreach (var weakPoint in distinct)
            {
                if (weakPoint.CatalogId is not { } catalogId || !catalogById.TryGetValue(catalogId, out var catalogRow))
                {
                    continue;
                }

                var occurrences = await _weakPointRepository.ListOccurrencesWithErrorByWeakPointAsync(weakPoint.Id, cancellationToken);
                var historical = occurrences
                    .Select(o => new WeakPointHistoricalError(o.Snippet, o.ErrorList?.Explanation))
                    .ToList();
                requests.Add(new WeakPointDetectionCriteriaRequest(
                    weakPoint.Id, catalogRow.Code, catalogRow.Name, catalogRow.Description, historical));
            }

            if (requests.Count == 0)
            {
                return;
            }

            var generated = await _detectionCriteriaGenerator.GenerateAsync(examTypeId, requests, cancellationToken);
            foreach (var weakPoint in distinct)
            {
                if (generated.TryGetValue(weakPoint.Id, out var criteria))
                {
                    weakPoint.DetectionCriteria = criteria;
                }
            }
        }

        /// <summary>
        /// AI③ — the active weak points this submission did NOT touch are candidates for recheck
        /// against this submission's source text + translation, batched into one call. Skipped
        /// entirely when there is nothing to check (no untouched active weak points, or the
        /// submission/question text can't be loaded). Never writes error_list, RecurrenceCount or
        /// OccurrenceSubmissionCount — see this class's doc comment.
        /// </summary>
        private async Task RecheckUntouchedActiveWeakPointsAsync(
            GenerateWeakPointsCommand gradedEvent,
            DateTimeOffset now,
            HashSet<Guid> touchedWeakPointIds,
            CancellationToken cancellationToken)
        {
            var activeWeakPoints = await _weakPointRepository.ListByUserAsync(gradedEvent.UserId, WeakPointStatus.active, cancellationToken);
            var candidates = activeWeakPoints
                .Where(w => !touchedWeakPointIds.Contains(w.Id)
                    && w.CatalogId is not null
                    && !string.IsNullOrWhiteSpace(w.DetectionCriteria)
                    && w.Catalog is not null)
                .ToList();
            if (candidates.Count == 0)
            {
                return;
            }

            var sourceAndTranslation = await _submissionRepository.GetSourceAndTranslationAsync(gradedEvent.SubmissionId, cancellationToken);
            if (sourceAndTranslation is null)
            {
                return;
            }

            var recheckCandidates = candidates
                .Select(w => new WeakPointRecheckCandidate(w.Id, w.Catalog!.Code, w.DetectionCriteria!))
                .ToList();
            var outcomes = await _recheckService.RecheckAsync(
                gradedEvent.ExamTypeId, recheckCandidates, sourceAndTranslation.SourceText, sourceAndTranslation.TranslationText, cancellationToken);

            foreach (var weakPoint in candidates)
            {
                if (!outcomes.TryGetValue(weakPoint.Id, out var outcome))
                {
                    // Omitted from the result — leave status/streak untouched (contract: never throws, never guesses).
                    continue;
                }

                switch (outcome)
                {
                    case WeakPointRecheckOutcome.Resolved:
                        weakPoint.Status = WeakPointStatus.resolved;
                        weakPoint.ResolvedAt = now;
                        weakPoint.NoEvidenceStreak = 0;
                        break;
                    case WeakPointRecheckOutcome.StillWeak:
                        weakPoint.NoEvidenceStreak = 0;
                        break;
                    case WeakPointRecheckOutcome.NotPresent:
                        weakPoint.NoEvidenceStreak += 1;
                        if (weakPoint.NoEvidenceStreak >= NoEvidenceResolveThreshold)
                        {
                            weakPoint.Status = WeakPointStatus.resolved;
                            weakPoint.ResolvedAt = now;
                            weakPoint.NoEvidenceStreak = 0;
                        }

                        break;
                }
            }
        }

        /// <summary>
        /// Mints a <see cref="WeakPointCatalogStatus.proposed"/> catalog kind for a recurring
        /// legacy bucket (deterministic code from the representative error's dimension + category,
        /// so two learners hitting the same pattern converge on one row). Returns the existing
        /// row if one already carries that code. Null only when the representative lacks a
        /// dimension/category to key on. <see cref="WeakPointCatalog.CategoryId"/> is left null —
        /// a rule-promoted bucket doesn't know which of the 8 top-level categories it belongs
        /// under; admin triage assigns one when approving the proposal to active.
        /// </summary>
        private async Task<WeakPointCatalog?> PromoteLegacyBucketAsync(
            WeakPoint weakPoint, ErrorListItem representative, CancellationToken cancellationToken)
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

            var existing = await _weakPointRepository.GetCatalogByCodeAsync(code, cancellationToken);
            if (existing is not null)
            {
                return existing;
            }

            var name = weakPoint.Category ?? $"{representative.Dimension?.DimensionName} - {representative.ErrorTaxonomy?.CategoryName}";
            var proposed = new WeakPointCatalog
            {
                Id = Guid.NewGuid(),
                CategoryId = null,
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
