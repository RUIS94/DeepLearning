using System.Text.Json;
using System.Text.Json.Serialization;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Infrastructure.Ai
{
    /// <inheritdoc cref="IWeakPointClassifier"/>
    public class WeakPointClassifier : IWeakPointClassifier
    {
        private static readonly JsonSerializerOptions PayloadJsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly IExamConfigLoader _examConfigLoader;
        private readonly ILlmClientResolver _llmClientResolver;
        private readonly IAiCallRetryExecutor _aiCallRetryExecutor;
        private readonly IAiCallLogRepository _aiCallLogRepository;
        private readonly IUnitOfWork _unitOfWork;

        public WeakPointClassifier(
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

        public async Task<WeakPointClassificationResult> ClassifyAsync(
            Guid examTypeId,
            IReadOnlyList<WeakPointClassifierError> errors,
            IReadOnlyList<WeakPointCatalog> catalog,
            IReadOnlyList<ActiveWeakPointSummary> activeWeakPoints,
            CancellationToken cancellationToken = default)
        {
            if (errors.Count == 0 || catalog.Count == 0)
            {
                return WeakPointClassificationResult.Empty;
            }

            string prompt;
            try
            {
                var model = new
                {
                    Errors = errors.Select(e => new
                    {
                        ErrorId = e.ErrorListId.ToString(),
                        DimensionKey = e.DimensionKey,
                        ErrorCategoryKey = e.ErrorCategoryKey,
                        Severity = e.Severity.ToString(),
                        Snippet = e.Snippet,
                        Explanation = e.Explanation,
                    }),
                    Catalog = catalog.Select(c => new { Code = c.Code, Name = c.Name, Description = c.Description }),
                    ActiveWeakPoints = activeWeakPoints.Select(w => new
                    {
                        Code = w.CatalogCode,
                        PatternSummary = w.PatternSummary ?? string.Empty,
                    }),
                };
                prompt = await _examConfigLoader.BuildPromptAsync(
                    examTypeId, AiOperationType.weak_point_classification, model, cancellationToken);
            }
            catch
            {
                // Template missing / render error — degrade to the rule, silently.
                return WeakPointClassificationResult.Empty;
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                // No weak_point_classification template configured for this exam type.
                return WeakPointClassificationResult.Empty;
            }

            var aiCallLog = new AiCallLog
            {
                Id = Guid.NewGuid(),
                RequestType = AiOperationType.weak_point_classification,
                Status = CallStatus.calling,
                AttemptCount = 1,
                MaxRetries = 3,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            try
            {
                await _aiCallLogRepository.AddAsync(aiCallLog, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                var payload = await _aiCallRetryExecutor.ExecuteAsync(aiCallLog, async () =>
                {
                    var llmClient = await _llmClientResolver.GetActiveClientAsync(cancellationToken);
                    var completion = await llmClient.CompleteAsync(
                        // Temperature 0: classification into a fixed catalog should be stable.
                        new LlmCompletionRequest(SystemPrompt: null, UserPrompt: prompt, MaxTokens: 2048, Temperature: 0m),
                        cancellationToken);
                    aiCallLog.LatencyMs = completion.LatencyMs;
                    return ParsePayload(completion.Text);
                }, cancellationToken);

                var idByCode = catalog
                    .GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
                var codeByCode = catalog
                    .GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().Code, StringComparer.OrdinalIgnoreCase);
                var validErrorIds = errors.Select(e => e.ErrorListId).ToHashSet();

                var errorToCatalog = new Dictionary<Guid, Guid>();
                foreach (var assignment in payload.Assignments ?? [])
                {
                    if (Guid.TryParse(assignment.ErrorId, out var errorId)
                        && validErrorIds.Contains(errorId)
                        && !string.IsNullOrWhiteSpace(assignment.CatalogCode)
                        && idByCode.TryGetValue(assignment.CatalogCode.Trim(), out var catalogId))
                    {
                        errorToCatalog[errorId] = catalogId;
                    }
                }

                var summaries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in payload.Summaries ?? [])
                {
                    if (!string.IsNullOrWhiteSpace(s.CatalogCode)
                        && !string.IsNullOrWhiteSpace(s.PatternSummary)
                        && codeByCode.TryGetValue(s.CatalogCode.Trim(), out var canonicalCode))
                    {
                        summaries[canonicalCode] = s.PatternSummary!.Trim();
                    }
                }

                aiCallLog.Status = CallStatus.success;
                aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
                await _unitOfWork.SaveChangesAsync(CancellationToken.None);
                return new WeakPointClassificationResult(errorToCatalog, summaries);
            }
            catch (Exception ex)
            {
                // Degrade to the rule — never let classification break weak-point tracking.
                // Best-effort mark the log failed; swallow even that if it can't be written.
                try
                {
                    aiCallLog.Status = CallStatus.final_failure;
                    aiCallLog.LastErrorMessage = $"Weak-point classification failed: {ex.Message}";
                    aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
                    await _unitOfWork.SaveChangesAsync(CancellationToken.None);
                }
                catch
                {
                    // ignored
                }

                return WeakPointClassificationResult.Empty;
            }
        }

        private static ClassificationPayload ParsePayload(string rawText)
        {
            var json = StripMarkdownFence(rawText.Trim());
            return JsonSerializer.Deserialize<ClassificationPayload>(json, PayloadJsonOptions)
                ?? throw new InvalidOperationException("Weak-point classification response deserialized to null.");
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

        private class ClassificationPayload
        {
            public List<AssignmentPayload>? Assignments { get; set; }
            public List<SummaryPayload>? Summaries { get; set; }
        }

        private class AssignmentPayload
        {
            [JsonPropertyName("errorId")]
            public string ErrorId { get; set; } = string.Empty;

            [JsonPropertyName("catalogCode")]
            public string? CatalogCode { get; set; }
        }

        private class SummaryPayload
        {
            [JsonPropertyName("catalogCode")]
            public string? CatalogCode { get; set; }

            [JsonPropertyName("patternSummary")]
            public string? PatternSummary { get; set; }
        }
    }
}
