using System.Text.Json.Serialization;
using DeepLearning.Application.Common;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Infrastructure.Ai
{
    /// <inheritdoc cref="IVocabSemanticDriftService"/>
    public class VocabSemanticDriftService : IVocabSemanticDriftService
    {
        private readonly IExamConfigLoader _examConfigLoader;
        private readonly ILlmClientResolver _llmClientResolver;
        private readonly IAiCallRetryExecutor _aiCallRetryExecutor;
        private readonly IAiCallLogRepository _aiCallLogRepository;
        private readonly IUnitOfWork _unitOfWork;

        public VocabSemanticDriftService(
            IExamConfigLoader examConfigLoader,
            ILlmClientResolver llmClientResolver,
            IAiCallRetryExecutor aiCallRetryExecutor,
            IAiCallLogRepository aiCallLogRepository,
            IUnitOfWork unitOfWork)
        {
            _examConfigLoader = examConfigLoader;
            _llmClientResolver = llmClientResolver;
            _aiCallRetryExecutor = aiCallRetryExecutor;
            _aiCallLogRepository = aiCallLogRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<IReadOnlyDictionary<string, string>> AnalyzeAsync(
            Guid examTypeId,
            IReadOnlyList<VocabDriftItem> items,
            string sourceText,
            string taskType,
            CancellationToken cancellationToken = default)
        {
            var empty = new Dictionary<string, string>();
            if (items.Count == 0)
            {
                return empty;
            }

            string prompt;
            try
            {
                var model = new
                {
                    TaskType = taskType,
                    SourceText = sourceText,
                    Items = items.Select(i => new
                    {
                        EnglishExpr = i.EnglishExpr,
                        ThisChinese = i.ThisChinese,
                        ThisNote = i.ThisNote,
                        KnownSemantics = i.KnownSemantics,
                    }),
                };
                prompt = await _examConfigLoader.BuildPromptAsync(
                    examTypeId, AiOperationType.vocab_semantic_drift, model, cancellationToken);
            }
            catch
            {
                return empty;
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                return empty;
            }

            var aiCallLog = new AiCallLog
            {
                Id = Guid.NewGuid(),
                RequestType = AiOperationType.vocab_semantic_drift,
                Status = CallStatus.calling,
                AttemptCount = 1,
                MaxRetries = 3,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            try
            {
                await _aiCallLogRepository.AddAsync(aiCallLog, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var llmClient = await _llmClientResolver.GetActiveClientAsync(AiOperationType.vocab_semantic_drift, cancellationToken);
                var payload = await AdaptiveCompletionRunner.RunAsync(
                    _aiCallRetryExecutor,
                    llmClient,
                    aiCallLog,
                    prompt,
                    initialBudget: AiOutputBudget.ShortInitial,
                    maxBudget: AiOutputBudget.ShortMax,
                    parse: ParsePayload,
                    temperature: 0m,
                    cancellationToken: cancellationToken);

                // Map the AI's echoed englishExpr back to the caller's canonical_key.
                var keyByNormalized = items
                    .GroupBy(i => Normalize(i.EnglishExpr))
                    .ToDictionary(g => g.Key, g => g.First().CanonicalKey);

                var result = new Dictionary<string, string>();
                foreach (var r in payload.Results ?? [])
                {
                    if (r.Changed != true
                        || string.IsNullOrWhiteSpace(r.UpdatedSemantics)
                        || string.IsNullOrWhiteSpace(r.EnglishExpr)
                        || !keyByNormalized.TryGetValue(Normalize(r.EnglishExpr), out var canonicalKey))
                    {
                        continue;
                    }

                    result[canonicalKey] = r.UpdatedSemantics.Trim();
                }

                aiCallLog.Status = CallStatus.success;
                aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
                await _unitOfWork.SaveChangesAsync(CancellationToken.None);
                return result;
            }
            catch (Exception ex)
            {
                try
                {
                    aiCallLog.Status = CallStatus.final_failure;
                    aiCallLog.LastErrorMessage = $"Vocab semantic-drift analysis failed: {ex.Message}";
                    aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
                    await _unitOfWork.SaveChangesAsync(CancellationToken.None);
                }
                catch
                {
                    // ignored
                }

                return empty;
            }
        }

        private static string Normalize(string value)
            => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

        private static DriftPayload ParsePayload(string rawText) => LlmJson.Parse<DriftPayload>(rawText);

        private class DriftPayload
        {
            public List<DriftResultPayload>? Results { get; set; }
        }

        private class DriftResultPayload
        {
            [JsonPropertyName("englishExpr")]
            public string? EnglishExpr { get; set; }

            [JsonPropertyName("changed")]
            public bool? Changed { get; set; }

            [JsonPropertyName("updatedSemantics")]
            public string? UpdatedSemantics { get; set; }
        }
    }
}
