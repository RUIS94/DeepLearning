using System.Text.Json;
using DeepLearning.Application.Common;
using DeepLearning.Application.Interfaces;
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
        private static readonly JsonSerializerOptions PayloadJsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly IExamTypeRepository _examTypeRepository;
        private readonly IQuestionRepository _questionRepository;
        private readonly IReferenceTranslationRepository _referenceTranslationRepository;
        private readonly IReviewLibraryRepository _reviewLibraryRepository;
        private readonly IAiCallLogRepository _aiCallLogRepository;
        private readonly IExamConfigLoader _examConfigLoader;
        private readonly ILlmClientResolver _llmClientResolver;
        private readonly IAiCallRetryExecutor _aiCallRetryExecutor;
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
            _unitOfWork = unitOfWork;
        }

        public async Task<GenerateDeepLearningContentResult> Handle(GenerateDeepLearningContentCommand request, CancellationToken cancellationToken)
        {
            _ = await _examTypeRepository.GetByIdAsync(request.ExamTypeId, cancellationToken)
                ?? throw new NotFoundException(nameof(ExamType), request.ExamTypeId);

            var question = await _questionRepository.GetByIdAsync(request.QuestionId, cancellationToken)
                ?? throw new NotFoundException(nameof(Question), request.QuestionId);

            var existing = await _referenceTranslationRepository.GetByQuestionIdAsync(question.Id, cancellationToken);
            if (existing is not null)
            {
                var cachedPatterns = await _reviewLibraryRepository.GetPatternsByQuestionIdAsync(question.Id, cancellationToken);
                var cachedVocab = await _reviewLibraryRepository.GetVocabByQuestionIdAsync(question.Id, cancellationToken);
                return ToResult(existing, cachedPatterns, cachedVocab, wasCached: true);
            }

            var aiCallLog = new AiCallLog
            {
                Id = Guid.NewGuid(),
                RequestType = AiOperationType.deep_learning,
                RelatedId = question.Id,
                Status = CallStatus.calling,
                AttemptCount = 1,
                MaxRetries = 3,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await _aiCallLogRepository.AddAsync(aiCallLog, cancellationToken);
            // Persisted up front so the log survives even if the LLM call below never returns.
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            DeepLearningPayload payload;
            try
            {
                // Deliberately just TaskType + SourceText + the cross-question dedup feed — see
                // the isolation note in the class doc comment. No submission content, no
                // grading_results, no meaning_checkpoints. PriorVocab is expressions accumulated
                // from OTHER questions that literally recur in this source text (design doc §9):
                // the prompt is told to re-explain them in this context, not repeat the old
                // entry. Inside the try so a failure here (query, template render) still routes
                // through FailAsync instead of leaving the call log stuck at 'calling'.
                var priorVocab = await _reviewLibraryRepository.ListPriorVocabForSourceAsync(question.SourceText, 40, cancellationToken);
                var templateModel = new
                {
                    TaskType = question.TaskType.ToString(),
                    SourceText = question.SourceText,
                    PriorVocab = priorVocab.Select(v => new
                    {
                        EnglishExpr = v.EnglishExpr,
                        ChineseEquiv = v.ChineseEquiv,
                        ContextNote = v.ContextNote,
                    }),
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
            try
            {
                referenceTranslation = new ReferenceTranslation
                {
                    Id = Guid.NewGuid(),
                    QuestionId = question.Id,
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
                    CanonicalKey = NormalizeCanonicalKey(p.PatternName),
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
                    CanonicalKey = NormalizeCanonicalKey(v.EnglishExpr),
                    CreatedAt = DateTimeOffset.UtcNow,
                }).ToList();
                await _reviewLibraryRepository.AddVocabAsync(vocab, cancellationToken);

                aiCallLog.Status = CallStatus.success;
                aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;

                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                await FailAsync(aiCallLog, $"Failed to persist deep learning content: {ex.Message}");
                throw new AiCallFailedException($"Deep learning content could not be used: {ex.Message}", ex);
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
                referenceTranslation.ReferenceText,
                referenceTranslation.ComparisonNotes,
                patterns.Select(p => new SentencePatternItem(p.Id, p.PatternName, p.ExampleSentence, p.BreakdownSteps, p.Variants, p.Domain, p.Scenario, p.FrequencyTag)).ToList(),
                vocab.Select(v => new VocabExpressionItem(v.Id, v.EnglishExpr, v.ChineseEquiv, v.ContextNote, v.Category, v.Domain, v.Scenario, v.FrequencyTag, v.LiteralTranslatable)).ToList(),
                wasCached);

        private static DeepLearningPayload ParsePayload(string rawText)
        {
            var json = StripMarkdownFence(rawText.Trim());
            return JsonSerializer.Deserialize<DeepLearningPayload>(json, PayloadJsonOptions)
                ?? throw new InvalidOperationException("Deserialized to null.");
        }

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

        /// <summary>
        /// lower-case, trim, collapse internal whitespace to single spaces, cap at the
        /// canonical_key column length. Null/blank -&gt; null.
        /// </summary>
        private static string? NormalizeCanonicalKey(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var collapsed = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            var normalized = collapsed.ToLowerInvariant();
            return normalized.Length > 255 ? normalized[..255] : normalized;
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

        private class DeepLearningPayload
        {
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
