using System.Text.Json;
using System.Text.Json.Serialization;
using DeepLearning.Application.Common;
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
                    // Grouped by top-level category for the AI's benefit — 薄弱点分类与生命周期
                    // 管理_策划书.md §1's two-level taxonomy. Leaves without a category yet (a
                    // proposal awaiting admin triage) are listed separately so they still
                    // participate in matching without implying a category they don't have.
                    Categories = catalog
                        .Where(c => c.Category is not null)
                        .GroupBy(c => c.Category!.Code)
                        .Select(g => new
                        {
                            CategoryCode = g.Key,
                            CategoryName = g.First().Category!.Name,
                            Leaves = g.Select(c => new { Code = c.Code, Name = c.Name, Description = c.Description }),
                        }),
                    UncategorizedLeaves = catalog
                        .Where(c => c.Category is null)
                        .Select(c => new { Code = c.Code, Name = c.Name, Description = c.Description }),
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

                // Temperature 0: classification into a fixed catalog should be stable.
                // ThinkingEnabled deliberately NOT set here — whether thinking runs is an
                // admin-controlled default (LlmProviderSettings.ThinkingEnabled, on by
                // default), not something this call forces off. Note the interaction if
                // the admin leaves it on: Mimo, a common active provider, ignores
                // temperature and top_p entirely while deep thinking is on (forced to
                // 1.0 / 0.95), so Temperature: 0 has no effect in that case — a known
                // tradeoff the admin is accepting by leaving thinking enabled, not a bug.
                // Providers that declare no thinking parameter are unaffected either way.
                //
                // MaxTokens starts at 2048 but can double up to 8192 on a truncated attempt
                // (AdaptiveCompletionRunner) — a submission with many errors spanning many
                // distinct catalog codes (task two needs a pattern_summary per code touched)
                // can genuinely need more than 2048 tokens; confirmed truncating for real on
                // 2026-09-05 (23 errors / 11 codes, cut off mid-JSON at exactly 2048 tokens).
                var llmClient = await _llmClientResolver.GetActiveClientAsync(AiOperationType.weak_point_classification, cancellationToken);
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

                var idByCode = catalog
                    .GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
                var codeByCode = catalog
                    .GroupBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().Code, StringComparer.OrdinalIgnoreCase);
                var validErrorIds = errors.Select(e => e.ErrorListId).ToHashSet();

                var categoryCodes = catalog
                    .Where(c => c.Category is not null)
                    .Select(c => c.Category!.Code)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var existingCatalogCodes = idByCode.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

                var errorToCatalog = new Dictionary<Guid, Guid>();
                var proposedLeaves = new List<ProposedCatalogLeaf>();
                var proposedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var assignment in payload.Assignments ?? [])
                {
                    if (Guid.TryParse(assignment.ErrorId, out var errorId)
                        && validErrorIds.Contains(errorId)
                        && !string.IsNullOrWhiteSpace(assignment.CatalogCode)
                        && idByCode.TryGetValue(assignment.CatalogCode.Trim(), out var catalogId))
                    {
                        errorToCatalog[errorId] = catalogId;
                        continue;
                    }

                    // Only meaningful when the AI left catalogCode null (an error placed into an
                    // existing leaf never also proposes a new one) and the suggestion looks usable:
                    // a valid lower_snake_case code, naming an existing category, not already a
                    // catalog code or a duplicate of another proposal in this same response.
                    var proposal = assignment.ProposedNewLeaf;
                    if (proposal is null
                        || string.IsNullOrWhiteSpace(proposal.Code)
                        || string.IsNullOrWhiteSpace(proposal.CategoryCode)
                        || string.IsNullOrWhiteSpace(proposal.Name)
                        || string.IsNullOrWhiteSpace(proposal.Description))
                    {
                        continue;
                    }

                    var code = proposal.Code.Trim();
                    if (!System.Text.RegularExpressions.Regex.IsMatch(code, "^[a-z0-9_]+$")
                        || existingCatalogCodes.Contains(code)
                        || !proposedCodes.Add(code)
                        || !categoryCodes.Contains(proposal.CategoryCode.Trim()))
                    {
                        continue;
                    }

                    proposedLeaves.Add(new ProposedCatalogLeaf(
                        proposal.CategoryCode.Trim(), code, proposal.Name.Trim(), proposal.Description.Trim()));
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
                return new WeakPointClassificationResult(errorToCatalog, summaries, proposedLeaves);
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
            var json = PromptJsonHelper.StripMarkdownFence(rawText.Trim());
            return JsonSerializer.Deserialize<ClassificationPayload>(json, PayloadJsonOptions)
                ?? throw new InvalidOperationException("Weak-point classification response deserialized to null.");
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

            [JsonPropertyName("proposedNewLeaf")]
            public ProposedNewLeafPayload? ProposedNewLeaf { get; set; }
        }

        private class ProposedNewLeafPayload
        {
            [JsonPropertyName("categoryCode")]
            public string? CategoryCode { get; set; }

            [JsonPropertyName("code")]
            public string? Code { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("description")]
            public string? Description { get; set; }
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
