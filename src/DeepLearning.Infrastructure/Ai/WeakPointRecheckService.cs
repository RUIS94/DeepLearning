using System.Text.Json.Serialization;
using DeepLearning.Application.Common;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Infrastructure.Ai
{
    /// <inheritdoc cref="IWeakPointRecheckService"/>
    public class WeakPointRecheckService : IWeakPointRecheckService
    {
        private readonly IExamConfigLoader _examConfigLoader;
        private readonly ILlmClientResolver _llmClientResolver;
        private readonly IAiCallRetryExecutor _aiCallRetryExecutor;
        private readonly IAiCallLogRepository _aiCallLogRepository;
        private readonly IUnitOfWork _unitOfWork;

        public WeakPointRecheckService(
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

        public async Task<IReadOnlyDictionary<Guid, WeakPointRecheckOutcome>> RecheckAsync(
            Guid examTypeId,
            IReadOnlyList<WeakPointRecheckCandidate> candidates,
            string sourceText,
            string translationText,
            CancellationToken cancellationToken = default)
        {
            if (candidates.Count == 0)
            {
                return new Dictionary<Guid, WeakPointRecheckOutcome>();
            }

            string prompt;
            try
            {
                var model = new
                {
                    Candidates = candidates.Select(c => new
                    {
                        CatalogCode = c.CatalogCode,
                        DetectionCriteria = c.DetectionCriteria,
                    }),
                    SourceText = sourceText,
                    TranslationText = translationText,
                };
                prompt = await _examConfigLoader.BuildPromptAsync(
                    examTypeId, AiOperationType.weak_point_recheck, model, cancellationToken);
            }
            catch
            {
                return new Dictionary<Guid, WeakPointRecheckOutcome>();
            }

            if (string.IsNullOrWhiteSpace(prompt))
            {
                return new Dictionary<Guid, WeakPointRecheckOutcome>();
            }

            return await AiCallScope.RunAsync(
                _aiCallLogRepository,
                _unitOfWork,
                AiOperationType.weak_point_recheck,
                async aiCallLog =>
                {
                    // ThinkingEnabled left unset — follows the admin-configured
                    // LlmProviderSettings.ThinkingEnabled default (on unless turned off), not
                    // forced off here. See WeakPointClassifier's fuller note on the
                    // temperature/thinking interaction for providers like Mimo.
                    //
                    // MaxTokens can double up to 8192 on a truncated attempt (AdaptiveCompletionRunner)
                    // rather than staying fixed — see WeakPointClassifier's note on the 2026-09-05
                    // truncation incident this guards against.
                    var llmClient = await _llmClientResolver.GetActiveClientAsync(AiOperationType.weak_point_recheck, cancellationToken);
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

                    var codeToWeakPointId = candidates
                        .GroupBy(c => c.CatalogCode, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.First().WeakPointId, StringComparer.OrdinalIgnoreCase);

                    var result = new Dictionary<Guid, WeakPointRecheckOutcome>();
                    foreach (var item in payload.Results ?? [])
                    {
                        if (string.IsNullOrWhiteSpace(item.CatalogCode)
                            || !codeToWeakPointId.TryGetValue(item.CatalogCode.Trim(), out var weakPointId))
                        {
                            continue;
                        }

                        var outcome = item.Outcome?.Trim().ToLowerInvariant() switch
                        {
                            "resolved" => WeakPointRecheckOutcome.Resolved,
                            "still_weak" => WeakPointRecheckOutcome.StillWeak,
                            "not_present" => WeakPointRecheckOutcome.NotPresent,
                            _ => (WeakPointRecheckOutcome?)null,
                        };
                        if (outcome is { } o)
                        {
                            result[weakPointId] = o;
                        }
                    }

                    return result;
                },
                onFailure: _ => new Dictionary<Guid, WeakPointRecheckOutcome>(),
                failureMessagePrefix: "Weak-point recheck failed",
                cancellationToken);
        }

        private static RecheckPayload ParsePayload(string rawText) => LlmJson.Parse<RecheckPayload>(rawText);

        private class RecheckPayload
        {
            public List<RecheckResultPayload>? Results { get; set; }
        }

        private class RecheckResultPayload
        {
            [JsonPropertyName("catalogCode")]
            public string? CatalogCode { get; set; }

            [JsonPropertyName("outcome")]
            public string? Outcome { get; set; }
        }
    }
}
