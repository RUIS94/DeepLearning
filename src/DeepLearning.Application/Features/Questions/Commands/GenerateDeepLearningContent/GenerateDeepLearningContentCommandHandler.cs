using System.Text.Json;
using DeepLearning.Application.Common;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Common;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.Questions.Commands.GenerateDeepLearningContent
{
    /// <summary>
    /// Design doc §10.2's isolation guarantee, mirrored the other direction from grading: the
    /// prompt built here only ever carries Question.SourceText (+ TaskType) — never a
    /// submission's content, its grading_results, or meaning_checkpoints — so a generated
    /// reference translation can never be contaminated by, or leak into, any one user's specific
    /// answer. One AI call produces the reference translation, general technique/pitfall notes,
    /// and any notable sentence patterns/vocab expressions together (design doc §2.1 displays all
    /// three at the same "深入学习" step). Idempotent per Question: `reference_translations` has
    /// no submission_id/user_id column (design doc §6.9), so it is generated once per Question
    /// and reused by every user who reaches this question's "深入学习" step — a second call
    /// returns the cached row instead of spending a second AI call.
    /// </summary>
    public class GenerateDeepLearningContentCommandHandler : IRequestHandler<GenerateDeepLearningContentCommand, GenerateDeepLearningContentResult>
    {
        private readonly IExamTypeRepository _examTypeRepository;
        private readonly IQuestionRepository _questionRepository;
        private readonly IReferenceTranslationRepository _referenceTranslationRepository;
        private readonly IReviewLibraryRepository _reviewLibraryRepository;
        private readonly IAiCallLogRepository _aiCallLogRepository;
        private readonly IExamConfigLoader _examConfigLoader;
        private readonly ILlmClientResolver _llmClientResolver;
        private readonly IAiCallRetryExecutor _aiCallRetryExecutor;
        private readonly IVocabGlossaryQueue _vocabGlossaryQueue;
        private readonly IUnitOfWork _unitOfWork;

        public GenerateDeepLearningContentCommandHandler(
            IExamTypeRepository examTypeRepository,
            IQuestionRepository questionRepository,
            IReferenceTranslationRepository referenceTranslationRepository,
            IReviewLibraryRepository reviewLibraryRepository,
            IAiCallLogRepository aiCallLogRepository,
            IExamConfigLoader examConfigLoader,
            ILlmClientResolver llmClientResolver,
            IAiCallRetryExecutor aiCallRetryExecutor,
            IVocabGlossaryQueue vocabGlossaryQueue,
            IUnitOfWork unitOfWork)
        {
            _examTypeRepository = examTypeRepository;
            _questionRepository = questionRepository;
            _referenceTranslationRepository = referenceTranslationRepository;
            _reviewLibraryRepository = reviewLibraryRepository;
            _aiCallLogRepository = aiCallLogRepository;
            _examConfigLoader = examConfigLoader;
            _llmClientResolver = llmClientResolver;
            _aiCallRetryExecutor = aiCallRetryExecutor;
            _vocabGlossaryQueue = vocabGlossaryQueue;
            _unitOfWork = unitOfWork;
        }

        public async Task<GenerateDeepLearningContentResult> Handle(GenerateDeepLearningContentCommand request, CancellationToken cancellationToken)
        {
            await _examTypeRepository.GetByIdAsync(request.ExamTypeId, cancellationToken)
                .EnsureFoundAsync(nameof(ExamType), request.ExamTypeId);

            var question = await _questionRepository.GetByIdAsync(request.QuestionId, cancellationToken)
                ?? throw new NotFoundException(nameof(Question), request.QuestionId);

            var existing = await _referenceTranslationRepository.GetByQuestionIdAsync(question.Id, cancellationToken);
            if (existing is not null)
            {
                var cachedPatterns = await _reviewLibraryRepository.GetPatternsByQuestionIdAsync(question.Id, cancellationToken);
                var cachedVocab = await _reviewLibraryRepository.GetVocabByQuestionIdAsync(question.Id, cancellationToken);
                return ToResult(existing, cachedPatterns, cachedVocab, wasCached: true);
            }

            var aiCallLog = AiCallLogFactory.New(AiOperationType.deep_learning, question.Id);
            await _aiCallLogRepository.AddAsync(aiCallLog, cancellationToken);
            // Persisted up front so the log survives even if the LLM call below never returns.
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            DeepLearningPayload payload;
            try
            {
                // Deliberately just TaskType + SourceText — see the isolation note in the class
                // doc comment. No submission content, no grading_results, no meaning_checkpoints.
                // Cross-question vocab handling was pulled out of this call on purpose: dedup is
                // the backend's job (merge on canonical_key), and re-explaining a recurring term
                // in a new context is meant to be its own small AI call, not a growing pile of
                // prior-entry preamble stapled to this four-part generation. Kept inside the try
                // so a failure here (template render) still routes through FailAsync instead of
                // leaving the call log stuck at 'calling'.
                var templateModel = new
                {
                    TaskType = question.TaskType.ToString(),
                    // The source article's own English title (questions.title, author-supplied —
                    // not the user's). Without it the reference translation renders the title
                    // blind and the pattern/vocab material can't draw on it. Same branch shape as
                    // grading's template; empty when the source genuinely has none.
                    SourceTitle = question.Title,
                    SourceText = question.SourceText,
                };
                var prompt = await _examConfigLoader.BuildPromptAsync(request.ExamTypeId, AiOperationType.deep_learning, templateModel, cancellationToken);

                // Design doc §4.2's retry sub-state-machine: re-prompts (same prompt, fresh call)
                // up to aiCallLog.MaxRetries times when the AI's response fails structured-output
                // validation — distinct from Polly's transport-level retries inside CompleteAsync
                // itself, which already ran and gave up before this ever throws.
                // Deep learning is the largest single response in the system — reference
                // translation + notes + sentence patterns + a full vocab list — and a
                // thinking-enabled model also spends budget on reasoning. 4096 previously
                // truncated the JSON mid-vocab-array; 8192 fits the v4 template's capped list
                // sizes normally, but can now double to 16384 on a truncated attempt
                // (AdaptiveCompletionRunner) instead of just hoping 8192 is always enough.
                var llmClient = await _llmClientResolver.GetActiveClientAsync(AiOperationType.deep_learning, cancellationToken);
                payload = await AdaptiveCompletionRunner.RunAsync(
                    _aiCallRetryExecutor,
                    llmClient,
                    aiCallLog,
                    prompt,
                    initialBudget: AiOutputBudget.LongInitial,
                    maxBudget: AiOutputBudget.LongMax,
                    parse: ParsePayload,
                    validate: ValidatePayload,
                    // temperature 0: this is structured extraction (reference translation + notes
                    // + patterns + vocab as strict JSON), not a task that benefits from sampling
                    // variety — and a lower temperature markedly cuts mimo's malformed-JSON rate
                    // (unescaped inner quotes in comparisonNotes/contextNote) that was driving the
                    // reject-and-retry loop. Same choice as grading / weak_point_* / the drift call.
                    temperature: 0m,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                await FailAsync(aiCallLog, $"Failed after {aiCallLog.AttemptCount} attempt(s): {ex.Message}");
                throw new AiCallFailedException($"Deep learning content could not be used: {ex.Message}", ex);
            }

            ReferenceTranslation referenceTranslation;
            List<SentencePattern> patterns;
            List<VocabExpression> vocab;
            var anyRecurringExpression = false;
            try
            {
                referenceTranslation = new ReferenceTranslation
                {
                    Id = Guid.NewGuid(),
                    QuestionId = question.Id,
                    ReferenceTitle = string.IsNullOrWhiteSpace(payload.ReferenceTitle) ? null : payload.ReferenceTitle.Trim(),
                    ReferenceText = payload.ReferenceText,
                    ComparisonNotes = payload.ComparisonNotes.ValueKind == JsonValueKind.Undefined ? null : payload.ComparisonNotes.GetRawText(),
                    CreatedAt = DateTimeOffset.UtcNow,
                };
                await _referenceTranslationRepository.AddAsync(referenceTranslation, cancellationToken);

                patterns = (payload.SentencePatterns ?? []).Select(p => new SentencePattern
                {
                    Id = Guid.NewGuid(),
                    QuestionId = question.Id,
                    PatternName = p.PatternName,
                    ExampleSentence = p.ExampleSentence,
                    BreakdownSteps = p.BreakdownSteps.ValueKind == JsonValueKind.Undefined ? null : p.BreakdownSteps.GetRawText(),
                    Variants = p.Variants,
                    Domain = p.Domain,
                    Scenario = p.Scenario,
                    FrequencyTag = p.FrequencyTag,
                    CanonicalKey = TextNormalization.CanonicalKey(p.PatternName),
                    CreatedAt = DateTimeOffset.UtcNow,
                }).ToList();
                await _reviewLibraryRepository.AddPatternsAsync(patterns, cancellationToken);

                vocab = (payload.VocabExpressions ?? []).Select(v => new VocabExpression
                {
                    Id = Guid.NewGuid(),
                    QuestionId = question.Id,
                    EnglishExpr = v.EnglishExpr,
                    ChineseEquiv = v.ChineseEquiv,
                    ContextNote = v.ContextNote,
                    Category = v.Category,
                    Domain = v.Domain,
                    Scenario = v.Scenario,
                    FrequencyTag = v.FrequencyTag,
                    LiteralTranslatable = v.LiteralTranslatable,
                    CanonicalKey = TextNormalization.CanonicalKey(v.EnglishExpr),
                    CreatedAt = DateTimeOffset.UtcNow,
                }).ToList();
                await _reviewLibraryRepository.AddVocabAsync(vocab, cancellationToken);

                // vocab_glossary: one canonical row per distinct expression, seeded
                // deterministically on first sight. Every VocabExpression above stays a frozen
                // per-question snapshot; this is the separate living record the review library
                // reads and the vocab_semantic_drift job maintains. A recurrence (an expression
                // whose canonical key already had a glossary row) only bumps the counter here —
                // whether this passage adds a new *sense* is decided later, off the request
                // thread, by AnalyzeVocabSemanticDriftJob.
                var now = DateTimeOffset.UtcNow;
                var distinctVocab = vocab
                    .Where(v => v.CanonicalKey is not null)
                    .GroupBy(v => v.CanonicalKey!)
                    .Select(g => g.First())
                    .ToList();
                foreach (var v in distinctVocab)
                {
                    var glossary = await _reviewLibraryRepository.GetGlossaryEntryByCanonicalKeyAsync(v.CanonicalKey!, cancellationToken);
                    if (glossary is null)
                    {
                        await _reviewLibraryRepository.AddGlossaryEntryAsync(new VocabGlossaryEntry
                        {
                            Id = Guid.NewGuid(),
                            CanonicalKey = v.CanonicalKey!,
                            EnglishExpr = v.EnglishExpr,
                            AccumulatedSemantics = v.ContextNote ?? string.Empty,
                            ChineseEquiv = v.ChineseEquiv,
                            Category = v.Category,
                            Domain = v.Domain,
                            Scenario = v.Scenario,
                            FrequencyTag = v.FrequencyTag,
                            FirstSeenQuestionId = question.Id,
                            CreatedAt = now,
                            UpdatedAt = now,
                        }, cancellationToken);
                    }
                    else
                    {
                        glossary.OccurrenceCount += 1;
                        glossary.UpdatedAt = now;
                        anyRecurringExpression = true;
                    }
                }

                aiCallLog.Status = CallStatus.success;
                aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;

                // A concurrent first-time generation of a *different* question that seeds one of
                // these same brand-new canonical keys between our read above and this save is
                // rare (generation is once-per-question, cached after) and self-correcting: this
                // whole save rolls back, the user retries, and the second pass finds the row.
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await FailAsync(aiCallLog, $"Failed to persist deep learning content: {ex.Message}");
                throw new AiCallFailedException($"Deep learning content could not be used: {ex.Message}", ex);
            }

            if (anyRecurringExpression)
            {
                await _vocabGlossaryQueue.EnqueueAsync(question.Id, request.ExamTypeId, cancellationToken);
            }

            return ToResult(referenceTranslation, patterns, vocab, wasCached: false);
        }

        private async Task FailAsync(AiCallLog aiCallLog, string errorMessage)
        {
            aiCallLog.Status = CallStatus.final_failure;
            aiCallLog.LastErrorMessage = errorMessage;
            aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
            // CancellationToken.None on purpose: the usual trigger for FailAsync is the request
            // being cancelled (the browser aborting a slow generation / React Query retrying),
            // and the failing token was that same one — passing it here would cancel the status
            // write too and leave the ai_call_logs row stuck at 'calling' forever.
            await _unitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        private static GenerateDeepLearningContentResult ToResult(
            ReferenceTranslation referenceTranslation,
            List<SentencePattern> patterns,
            List<VocabExpression> vocab,
            bool wasCached) => new(
                referenceTranslation.QuestionId,
                referenceTranslation.ReferenceTitle,
                referenceTranslation.ReferenceText,
                referenceTranslation.ComparisonNotes,
                patterns.Select(p => new SentencePatternItem(p.Id, p.PatternName, p.ExampleSentence, p.BreakdownSteps, p.Variants, p.Domain, p.Scenario, p.FrequencyTag)).ToList(),
                vocab.Select(v => new VocabExpressionItem(v.Id, v.EnglishExpr, v.ChineseEquiv, v.ContextNote, v.Category, v.Domain, v.Scenario, v.FrequencyTag, v.LiteralTranslatable)).ToList(),
                wasCached);

        private static DeepLearningPayload ParsePayload(string rawText) => LlmJson.Parse<DeepLearningPayload>(rawText);

        /// <summary>
        /// Same "structured output is a hard constraint" philosophy as every other AI-orchestration
        /// handler in this codebase (design doc §10.3) — a malformed item is a rejected response,
        /// not a silently-dropped one.
        /// </summary>
        private static void ValidatePayload(DeepLearningPayload payload)
        {
            if (string.IsNullOrWhiteSpace(payload.ReferenceText))
            {
                throw new InvalidOperationException("referenceText must not be empty.");
            }

            foreach (var pattern in payload.SentencePatterns ?? [])
            {
                if (string.IsNullOrWhiteSpace(pattern.PatternName))
                {
                    throw new InvalidOperationException("Every sentencePatterns item must have a non-empty patternName.");
                }
            }

            foreach (var expr in payload.VocabExpressions ?? [])
            {
                if (string.IsNullOrWhiteSpace(expr.EnglishExpr))
                {
                    throw new InvalidOperationException("Every vocabExpressions item must have a non-empty englishExpr.");
                }
            }
        }

        private class DeepLearningPayload
        {
            public string? ReferenceTitle { get; set; }

            public string ReferenceText { get; set; } = string.Empty;

            public JsonElement ComparisonNotes { get; set; }

            public List<SentencePatternPayload>? SentencePatterns { get; set; }

            public List<VocabExpressionPayload>? VocabExpressions { get; set; }
        }

        private class SentencePatternPayload
        {
            public string PatternName { get; set; } = string.Empty;

            public string? ExampleSentence { get; set; }

            public JsonElement BreakdownSteps { get; set; }

            public string? Variants { get; set; }

            public string? Domain { get; set; }

            public string? Scenario { get; set; }

            public string? FrequencyTag { get; set; }
        }

        private class VocabExpressionPayload
        {
            public string EnglishExpr { get; set; } = string.Empty;

            public string? ChineseEquiv { get; set; }

            public string? ContextNote { get; set; }

            public string? Category { get; set; }

            public string? Domain { get; set; }

            public string? Scenario { get; set; }

            public string? FrequencyTag { get; set; }

            public bool? LiteralTranslatable { get; set; }
        }
    }
}
