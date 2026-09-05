using System.Text.Json;
using System.Text.Json.Serialization;
using DeepLearning.Application.Common;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Infrastructure.Ai
{
    /// <inheritdoc cref="IWeakPointDetectionCriteriaGenerator"/>
    public class WeakPointDetectionCriteriaGenerator : IWeakPointDetectionCriteriaGenerator
    {
        private static readonly JsonSerializerOptions PayloadJsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly IExamConfigLoader _examConfigLoader;
        private readonly ILlmClientResolver _llmClientResolver;
        private readonly IAiCallRetryExecutor _aiCallRetryExecutor;
        private readonly IAiCallLogRepository _aiCallLogRepository;
        private readonly IUnitOfWork _unitOfWork;

        public WeakPointDetectionCriteriaGenerator(
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

        public async Task<IReadOnlyDictionary<Guid, string>> GenerateAsync(
            Guid examTypeId,
            IReadOnlyList<WeakPointDetectionCriteriaRequest> requests,
            CancellationToken cancellationToken = default)
        {
            if (requests.Count == 0)
            {
                return new Dictionary<Guid, string>();
            }

            string prompt;
            try
            {
                var model = new
                {
                    WeakPoints = requests.Select(r => new
                    {
                        CatalogCode = r.CatalogCode,
                        CatalogName = r.CatalogName,
                        CatalogDescription = r.CatalogDescription,
                        HistoricalErrors = r.HistoricalErrors.Select(e => new
                        {
                            Snippet = e.Snippet,
                            Explanation = e.Explanation,
                        }),
                    }),
                };
                prompt = await _examConfigLoader.BuildPromptAsync(
                    examTypeId, AiOperationType.weak_point_detection_criteria, model, cancellationToken);
            }
            catch
            {
                return new Dictionary<Guid, string>();
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                return new Dictionary<Guid, string>();
            }

            var aiCallLog = new AiCallLog
            {
                Id = Guid.NewGuid(),
                RequestType = AiOperationType.weak_point_detection_criteria,
                Status = CallStatus.calling,
                AttemptCount = 1,
                MaxRetries = 3,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            try
            {
                await _aiCallLogRepository.AddAsync(aiCallLog, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // ThinkingEnabled left unset — follows the admin-configured
                // LlmProviderSettings.ThinkingEnabled default (on unless turned off), not
                // forced off here. See WeakPointClassifier's fuller note on the
                // temperature/thinking interaction for providers like Mimo.
                //
                // MaxTokens can double up to 8192 on a truncated attempt (AdaptiveCompletionRunner)
                // rather than staying fixed — see WeakPointClassifier's note on the 2026-09-05
                // truncation incident this guards against.
                var llmClient = await _llmClientResolver.GetActiveClientAsync(cancellationToken);
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

                var codeToWeakPointId = requests
                    .GroupBy(r => r.CatalogCode, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().WeakPointId, StringComparer.OrdinalIgnoreCase);

                var result = new Dictionary<Guid, string>();
                foreach (var item in payload.DetectionCriteria ?? [])
                {
                    if (!string.IsNullOrWhiteSpace(item.CatalogCode)
                        && !string.IsNullOrWhiteSpace(item.Criteria)
                        && codeToWeakPointId.TryGetValue(item.CatalogCode.Trim(), out var weakPointId))
                    {
                        result[weakPointId] = item.Criteria.Trim();
                    }
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
                    aiCallLog.LastErrorMessage = $"Weak-point detection-criteria generation failed: {ex.Message}";
                    aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
                    await _unitOfWork.SaveChangesAsync(CancellationToken.None);
                }
                catch
                {
                    // ignored
                }

                return new Dictionary<Guid, string>();
            }
        }

        private static DetectionCriteriaPayload ParsePayload(string rawText)
        {
            var json = PromptJsonHelper.StripMarkdownFence(rawText.Trim());
            return JsonSerializer.Deserialize<DetectionCriteriaPayload>(json, PayloadJsonOptions)
                ?? throw new InvalidOperationException("Weak-point detection-criteria response deserialized to null.");
        }

        private class DetectionCriteriaPayload
        {
            public List<DetectionCriteriaItemPayload>? DetectionCriteria { get; set; }
        }

        private class DetectionCriteriaItemPayload
        {
            [JsonPropertyName("catalogCode")]
            public string? CatalogCode { get; set; }

            [JsonPropertyName("criteria")]
            public string? Criteria { get; set; }
        }
    }
}
