using System.Text.Json;
using System.Text.Json.Serialization;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace DeepLearning.Application.Features.Questions.Commands.GenerateQuestion
{
    public class GenerateQuestionCommandHandler : IRequestHandler<GenerateQuestionCommand, GenerateQuestionResult>
    {
        private static readonly JsonSerializerOptions PayloadJsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly IExamTypeRepository _examTypeRepository;
        private readonly IUserRepository _userRepository;
        private readonly IQuestionRepository _questionRepository;
        private readonly IErrorTaxonomyRepository _errorTaxonomyRepository;
        private readonly IQuestionBankCategoryRepository _questionBankCategoryRepository;
        private readonly IGenerationPolicyRepository _generationPolicyRepository;
        private readonly IWeakPointRepository _weakPointRepository;
        private readonly IAiCallLogRepository _aiCallLogRepository;
        private readonly ISeedReferenceLinkRepository _seedReferenceLinkRepository;
        private readonly IExamConfigLoader _examConfigLoader;
        private readonly ILlmClientResolver _llmClientResolver;
        private readonly IAiCallRetryExecutor _aiCallRetryExecutor;
        private readonly IUnitOfWork _unitOfWork;

        public GenerateQuestionCommandHandler(
            IExamTypeRepository examTypeRepository,
            IUserRepository userRepository,
            IQuestionRepository questionRepository,
            IErrorTaxonomyRepository errorTaxonomyRepository,
            IQuestionBankCategoryRepository questionBankCategoryRepository,
            IGenerationPolicyRepository generationPolicyRepository,
            IWeakPointRepository weakPointRepository,
            IAiCallLogRepository aiCallLogRepository,
            ISeedReferenceLinkRepository seedReferenceLinkRepository,
            IExamConfigLoader examConfigLoader,
            ILlmClientResolver llmClientResolver,
            IAiCallRetryExecutor aiCallRetryExecutor,
            IUnitOfWork unitOfWork)
        {
            _examTypeRepository = examTypeRepository;
            _userRepository = userRepository;
            _questionRepository = questionRepository;
            _errorTaxonomyRepository = errorTaxonomyRepository;
            _questionBankCategoryRepository = questionBankCategoryRepository;
            _generationPolicyRepository = generationPolicyRepository;
            _weakPointRepository = weakPointRepository;
            _aiCallLogRepository = aiCallLogRepository;
            _seedReferenceLinkRepository = seedReferenceLinkRepository;
            _examConfigLoader = examConfigLoader;
            _llmClientResolver = llmClientResolver;
            _aiCallRetryExecutor = aiCallRetryExecutor;
            _unitOfWork = unitOfWork;
        }

        public async Task<GenerateQuestionResult> Handle(GenerateQuestionCommand request, CancellationToken cancellationToken)
        {
            _ = await _examTypeRepository.GetByIdAsync(request.ExamTypeId, cancellationToken)
                ?? throw new NotFoundException(nameof(ExamType), request.ExamTypeId);

            if (request.CreatedBy is { } createdBy)
            {
                _ = await _userRepository.GetByIdAsync(createdBy, cancellationToken)
                    ?? throw new NotFoundException(nameof(User), createdBy);
            }

            // An explicit CategoryId is mapped to the generated question (question_category_map)
            // below, so it must resolve to a real row — reject early rather than let the FK blow
            // up mid-persist. The resolved entity is kept: its name becomes a hard "use exactly
            // this domain" directive in the prompt (PinnedDomain).
            QuestionBankCategory? pinnedCategory = null;
            if (request.CategoryId is { } categoryId)
            {
                pinnedCategory = await _questionBankCategoryRepository.GetByIdAsync(categoryId, cancellationToken)
                    ?? throw new NotFoundException(nameof(QuestionBankCategory), categoryId);
            }

            var difficulty = request.Difficulty ?? await ResolveDifficultyAsync(request.ExamTypeId, cancellationToken);
            var errorTaxonomies = await _errorTaxonomyRepository.ListByExamTypeAsync(request.ExamTypeId, cancellationToken);
            // Injected into the prompt so the AI's brief.domain reuses an existing name instead of
            // inventing near-duplicates ("政府公告" / "政府通告" / "Government Notices"). Also reused
            // by ResolveTopicHintAsync and MapCategoriesAsync so this is the only DB read for it.
            var domainCategories = await _questionBankCategoryRepository.ListAsync(CategoryType.domain, cancellationToken);

            var (seedSamples, seedSelectionReason) = await ResolveSeedSamplesAsync(request, cancellationToken);
            var weakPointHint = await ResolveWeakPointHintAsync(request, cancellationToken);
            var topicHint = await ResolveTopicHintAsync(request, domainCategories, cancellationToken);

            var aiCallLog = new AiCallLog
            {
                Id = Guid.NewGuid(),
                RequestType = AiOperationType.question_gen,
                Status = CallStatus.calling,
                AttemptCount = 1,
                MaxRetries = 3,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await _aiCallLogRepository.AddAsync(aiCallLog, cancellationToken);
            // Persisted up front so the log survives even if the LLM call below never returns.
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var templateModel = new
            {
                Difficulty = difficulty.ToString(),
                TaskType = request.TaskType.ToString(),
                ErrorTaxonomies = errorTaxonomies.Select(t => new
                {
                    CategoryKey = t.CategoryKey,
                    CategoryName = t.CategoryName,
                    Description = t.Description,
                }),
                SeedSamples = seedSamples.Select(s => new
                {
                    Title = s.Title,
                    SourceText = s.SourceText,
                }),
                WeakPointHint = weakPointHint,
                TopicHint = topicHint,
                PinnedDomain = pinnedCategory?.Name,
                DomainCategories = domainCategories.Select(c => new { Name = c.Name }),
            };
            var prompt = await _examConfigLoader.BuildPromptAsync(
                request.ExamTypeId, AiOperationType.question_gen, templateModel, cancellationToken);

            GeneratedQuestionPayload payload;
            List<TaskBSeededError> seededErrors;
            try
            {
                // Design doc §4.2's retry sub-state-machine: re-prompts (same prompt, fresh call)
                // up to aiCallLog.MaxRetries times when the AI's response fails structured-output
                // validation — distinct from Polly's transport-level retries inside CompleteAsync
                // itself, which already ran and gave up before this ever throws.
                (payload, seededErrors) = await _aiCallRetryExecutor.ExecuteAsync(aiCallLog, async () =>
                {
                    var llmClient = await _llmClientResolver.GetActiveClientAsync(cancellationToken);
                    var completion = await llmClient.CompleteAsync(
                        new LlmCompletionRequest(SystemPrompt: null, UserPrompt: prompt, MaxTokens: 4096),
                        cancellationToken);
                    aiCallLog.LatencyMs = completion.LatencyMs;

                    var parsedPayload = ParsePayload(completion.Text);
                    var errors = request.TaskType == TaskType.B
                        ? ValidateAndBuildTaskBSeededErrors(parsedPayload, errorTaxonomies)
                        : [];
                    return (parsedPayload, errors);
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                await FailAsync(aiCallLog, $"Failed after {aiCallLog.AttemptCount} attempt(s): {ex.Message}", cancellationToken);
                throw new AiCallFailedException($"Claude response could not be used: {ex.Message}", ex);
            }

            var briefFields = ParseBriefFields(payload.Brief);

            var question = new Question
            {
                Id = Guid.NewGuid(),
                TaskType = request.TaskType,
                Difficulty = difficulty,
                Title = payload.Title,
                Brief = payload.Brief.ValueKind == JsonValueKind.Undefined ? null : payload.Brief.GetRawText(),
                BriefDomain = briefFields.Domain,
                BriefTextType = briefFields.TextType,
                BriefPurpose = briefFields.Purpose,
                BriefAudience = briefFields.Audience,
                SourceText = payload.SourceText,
                // Only meaningful for TaskB — payload.FlawedTranslationText is validated
                // non-empty by ValidateAndBuildTaskBSeededErrors above when TaskType==B.
                FlawedTranslationText = request.TaskType == TaskType.B ? payload.FlawedTranslationText : null,
                WordCount = payload.WordCount,
                Origin = QuestionOrigin.ai_generated,
                SourceType = SourceType.ai_generated,
                IsSeedReference = false,
                InBank = false,
                Visibility = Domain.Enums.Visibility.Private,
                CreatedBy = request.CreatedBy,
                CreatedAt = DateTimeOffset.UtcNow,
                IsActive = true,
            };
            await _questionRepository.AddAsync(question, cancellationToken);

            var checkpoints = (payload.MeaningCheckpoints ?? []).Select(c => new MeaningCheckpoint
            {
                Id = Guid.NewGuid(),
                QuestionId = question.Id,
                CheckpointText = c.CheckpointText,
                CheckpointType = c.CheckpointType,
                Importance = c.Importance,
                CreatedAt = DateTimeOffset.UtcNow,
            }).ToList();
            await _questionRepository.AddMeaningCheckpointsAsync(checkpoints, cancellationToken);

            foreach (var seededError in seededErrors)
            {
                seededError.QuestionId = question.Id;
            }
            await _questionRepository.AddSeededErrorsAsync(seededErrors, cancellationToken);

            await MapCategoriesAsync(question, pinnedCategory, briefFields.Domain, domainCategories, cancellationToken);

            if (seedSamples.Count > 0)
            {
                await _seedReferenceLinkRepository.AddRangeAsync(
                    seedSamples.Select(s => new SeedReferenceLink
                    {
                        Id = Guid.NewGuid(),
                        GeneratedQuestionId = question.Id,
                        SeedQuestionId = s.Id,
                        SimilarityReason = seedSelectionReason,
                        CreatedAt = DateTimeOffset.UtcNow,
                    }),
                    cancellationToken);
            }

            aiCallLog.Status = CallStatus.success;
            aiCallLog.RelatedId = question.Id;
            aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new GenerateQuestionResult(question.Id, question.TaskType, question.Difficulty, question.Title, question.CreatedAt);
        }

        private async Task<Difficulty> ResolveDifficultyAsync(Guid examTypeId, CancellationToken cancellationToken)
        {
            var policy = await _generationPolicyRepository.GetByKeyAsync(examTypeId, "difficulty_distribution", cancellationToken);
            var weights = policy is not null
                ? DifficultyDistributionSelector.ParseWeights(policy.PolicyValue)
                : DifficultyDistributionSelector.DefaultWeights;

            return DifficultyDistributionSelector.Select(weights, Random.Shared.NextDouble());
        }

        /// <summary>
        /// Design doc §10.5's opt-in weak-point targeting (see WeakPointTargetingSelector and
        /// GenerateQuestionCommand's own doc comment). Returns null (no hint injected into the
        /// prompt) unless the caller asked for it, supplied a user, that user has an active weak
        /// point on file, AND the policy-weighted dice roll actually says to target this call —
        /// most calls with TargetWeakPoints=true still won't get a hint, by design.
        /// </summary>
        private async Task<string?> ResolveWeakPointHintAsync(GenerateQuestionCommand request, CancellationToken cancellationToken)
        {
            if (!request.TargetWeakPoints || request.CreatedBy is not { } userId)
            {
                return null;
            }

            var policy = await _generationPolicyRepository.GetByKeyAsync(request.ExamTypeId, "weak_point_targeting_ratio", cancellationToken);
            var ratio = policy is not null
                ? WeakPointTargetingSelector.ParseRatio(policy.PolicyValue)
                : WeakPointTargetingSelector.DefaultWeakPointRatio;

            if (!WeakPointTargetingSelector.ShouldTarget(ratio, Random.Shared.NextDouble()))
            {
                return null;
            }

            var activeWeakPoints = await _weakPointRepository.ListByUserAsync(userId, WeakPointStatus.active, cancellationToken);
            var topWeakPoint = activeWeakPoints
                // Priority is declared { high, medium, low } — high is ordinal 0, so ascending
                // order puts it first (see AGENTS.md's note on this same enum-ordinal landmine).
                .OrderBy(w => w.Priority)
                .ThenByDescending(w => w.LastSeenAt)
                .FirstOrDefault();

            // Catalog name (e.g. "数字/统计类陷阱") is the useful hint; Category is null once mapped.
            return topWeakPoint?.Catalog?.Name ?? topWeakPoint?.Category;
        }

        /// <summary>
        /// Design doc §11.2 Step 8's "题材可随机" — the SOFT random nudge, only for when the caller
        /// did NOT pin a CategoryId (a pinned one is a hard directive handled as PinnedDomain, not
        /// here). A topic_distribution-weighted roll decides whether to pick one existing domain
        /// category at random as a hint; returns null (the AI picks from the injected domain list
        /// on its own) when the roll misses or no domain categories exist yet.
        /// </summary>
        private async Task<string?> ResolveTopicHintAsync(
            GenerateQuestionCommand request, List<QuestionBankCategory> domainCategories, CancellationToken cancellationToken)
        {
            if (request.CategoryId is not null)
            {
                return null;
            }

            var policy = await _generationPolicyRepository.GetByKeyAsync(request.ExamTypeId, "topic_distribution", cancellationToken);
            var ratio = policy is not null
                ? TopicDistributionSelector.ParseRatio(policy.PolicyValue)
                : TopicDistributionSelector.DefaultTopicRandomRatio;

            if (!TopicDistributionSelector.ShouldPick(ratio, Random.Shared.NextDouble()))
            {
                return null;
            }

            return domainCategories.Count == 0 ? null : domainCategories[Random.Shared.Next(domainCategories.Count)].Name;
        }

        /// <summary>
        /// Wires the generated question into question_category_map with exactly one domain link.
        /// When the caller pinned a category, THAT is the question's domain — link only it, and do
        /// not also derive a second one from the AI's brief.domain (which may not match the pinned
        /// name and would otherwise double-link the question). When nothing was pinned, find-or-
        /// create a domain category from brief.domain instead, matched case-insensitively against
        /// the existing list so the AI reusing a listed name doesn't spawn a near-duplicate.
        /// </summary>
        private async Task MapCategoriesAsync(
            Question question,
            QuestionBankCategory? pinnedCategory,
            string? briefDomain,
            List<QuestionBankCategory> domainCategories,
            CancellationToken cancellationToken)
        {
            Guid categoryId;

            if (pinnedCategory is not null)
            {
                categoryId = pinnedCategory.Id;
            }
            else
            {
                var domainName = briefDomain?.Trim();
                // Only turn a domain into a bank category when it reads like a label, not a
                // paragraph — an over-long value is still stored in questions.brief_domain, it
                // just doesn't pollute question_bank_categories.
                if (string.IsNullOrEmpty(domainName) || domainName.Length > CategoryNameMaxLength)
                {
                    return;
                }

                var match = domainCategories.FirstOrDefault(
                    c => string.Equals(c.Name.Trim(), domainName, StringComparison.OrdinalIgnoreCase));
                if (match is null)
                {
                    match = new QuestionBankCategory
                    {
                        Id = Guid.NewGuid(),
                        CategoryType = CategoryType.domain,
                        Name = domainName,
                        CreatedAt = DateTimeOffset.UtcNow,
                    };
                    await _questionBankCategoryRepository.AddAsync(match, cancellationToken);
                }

                categoryId = match.Id;
            }

            await _questionRepository.AddCategoryMapAsync(new QuestionCategoryMap
            {
                Id = Guid.NewGuid(),
                QuestionId = question.Id,
                CategoryId = categoryId,
            }, cancellationToken);
        }

        // Backstop caps for the AI's brief text. The prompt already asks for short English
        // values (domain from the catalogue, textType from a fixed list, purpose <= 12 words,
        // audience <= 8 words); these truncate a misbehaving response before it reaches the DB
        // (and before an over-long purpose/audience can break the UI or leak the passage's gist).
        // Kept well under the actual column limits from QuestionConfiguration.
        private const int BriefDomainMaxLength = 100;
        private const int BriefTextTypeMaxLength = 60;
        private const int BriefAudienceMaxLength = 150;
        private const int BriefPurposeMaxLength = 300;
        private const int CategoryNameMaxLength = 100;

        /// <summary>
        /// Pulls domain / text type / purpose / audience out of the AI's brief JSON into the
        /// structured columns. Tolerates both the English keys the current template asks for
        /// (domain/textType/purpose/audience) and the Chinese keys older rows used
        /// (领域/文本类型/目的/受众). All fields optional; each is trimmed and hard-capped to its
        /// column length so an over-verbose AI response can't fail the whole generation.
        /// </summary>
        private static (string? Domain, string? TextType, string? Purpose, string? Audience) ParseBriefFields(JsonElement brief)
        {
            if (brief.ValueKind != JsonValueKind.Object)
            {
                return (null, null, null, null);
            }

            string? Read(int maxLength, params string[] keys)
            {
                foreach (var key in keys)
                {
                    if (brief.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
                    {
                        var text = value.GetString()?.Trim();
                        if (!string.IsNullOrEmpty(text))
                        {
                            return text.Length > maxLength ? text[..maxLength] : text;
                        }
                    }
                }

                return null;
            }

            return (
                Read(BriefDomainMaxLength, "domain", "topic", "领域"),
                Read(BriefTextTypeMaxLength, "textType", "text_type", "文本类型"),
                Read(BriefPurposeMaxLength, "purpose", "目的"),
                Read(BriefAudienceMaxLength, "audience", "受众"));
        }

        /// <summary>
        /// Few-shot real-exam samples are strictly opt-in: the "真题参考样本" prompt block is
        /// populated ONLY from the caller's explicit SeedQuestionIds, in the given order. With no
        /// SeedQuestionIds the block stays empty. (Previously the handler auto-pulled up to 3
        /// IsSeedReference questions matching task type + difficulty, so the block appeared on
        /// every generation whether the caller asked for it or not — user-requested change.)
        /// Every id must resolve to a real, IsSeedReference=true Question or the whole request is
        /// rejected (404/400) before any AI call is made — design doc §10.3's "hard constraint in
        /// code" philosophy: a manually "referenced" question must actually be a real-exam seed,
        /// or seed_reference_links' audit trail stops meaning what it says.
        /// </summary>
        private async Task<(List<Question> Samples, string Reason)> ResolveSeedSamplesAsync(
            GenerateQuestionCommand request, CancellationToken cancellationToken)
        {
            if (request.SeedQuestionIds is not { Count: > 0 } seedQuestionIds)
            {
                return ([], string.Empty);
            }

            var found = await _questionRepository.ListByIdsAsync(seedQuestionIds, cancellationToken);
            var byId = found.ToDictionary(x => x.Id);

            foreach (var id in seedQuestionIds)
            {
                if (!byId.TryGetValue(id, out var seedQuestion))
                {
                    throw new NotFoundException(nameof(Question), id);
                }

                if (!seedQuestion.IsSeedReference)
                {
                    throw new ValidationException(new[]
                    {
                        new ValidationFailure(
                            nameof(GenerateQuestionCommand.SeedQuestionIds),
                            $"Question '{id}' is not a seed reference (IsSeedReference=false) and cannot be manually specified as generation reference."),
                    });
                }
            }

            // Preserve the caller's own ordering rather than whatever order the DB returns.
            var ordered = seedQuestionIds.Select(id => byId[id]).ToList();
            return (ordered, "manually specified by caller");
        }

        private async Task FailAsync(AiCallLog aiCallLog, string errorMessage, CancellationToken cancellationToken)
        {
            aiCallLog.Status = CallStatus.final_failure;
            aiCallLog.LastErrorMessage = errorMessage;
            aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private static GeneratedQuestionPayload ParsePayload(string rawText)
        {
            var json = StripMarkdownFence(rawText.Trim());
            return JsonSerializer.Deserialize<GeneratedQuestionPayload>(json, PayloadJsonOptions)
                ?? throw new InvalidOperationException("Deserialized to null.");
        }

        /// <summary>
        /// Same structured-output-is-a-hard-constraint philosophy as GradeSubmissionCommandHandler's
        /// ValidatePayload (design doc §10.3): a TaskB response must actually carry a
        /// flawedTranslationText and at least one seededError, every seededError's errorCategory
        /// must be a known taxonomy for this exam type, and positions must fit inside the flawed
        /// text with no overlaps — the same rules ImportUserQuestionValidator enforces for
        /// manually-entered TaskB questions, just applied to the AI's own output instead of a
        /// human's.
        /// </summary>
        private static List<TaskBSeededError> ValidateAndBuildTaskBSeededErrors(GeneratedQuestionPayload payload, List<ErrorTaxonomy> errorTaxonomies)
        {
            if (string.IsNullOrEmpty(payload.FlawedTranslationText))
            {
                throw new InvalidOperationException("TaskB generation must include a non-empty flawedTranslationText.");
            }

            if (payload.SeededErrors is not { Count: > 0 })
            {
                throw new InvalidOperationException("TaskB generation must include at least one seededError.");
            }

            var taxonomiesByKey = errorTaxonomies.ToDictionary(x => x.CategoryKey);
            var flawedLength = payload.FlawedTranslationText.Length;
            var sorted = payload.SeededErrors.OrderBy(e => e.PositionStart).ToList();

            for (var i = 0; i < sorted.Count; i++)
            {
                var error = sorted[i];

                if (error.PositionStart < 0 || error.PositionEnd <= error.PositionStart || error.PositionEnd > flawedLength)
                {
                    throw new InvalidOperationException(
                        $"seededError position [{error.PositionStart},{error.PositionEnd}) is out of bounds for the {flawedLength}-character flawedTranslationText.");
                }

                if (!taxonomiesByKey.ContainsKey(error.ErrorCategory))
                {
                    throw new InvalidOperationException($"seededError errorCategory '{error.ErrorCategory}' is not a known error taxonomy for this exam type.");
                }

                if (i > 0 && error.PositionStart < sorted[i - 1].PositionEnd)
                {
                    throw new InvalidOperationException("seededError position ranges must not overlap.");
                }
            }

            // QuestionId is filled in by the caller once the Question's own Id is known.
            return sorted.Select(e => new TaskBSeededError
            {
                Id = Guid.NewGuid(),
                PositionStart = e.PositionStart,
                PositionEnd = e.PositionEnd,
                ErrorTaxonomyId = taxonomiesByKey[e.ErrorCategory].Id,
                CorrectReferenceText = e.CorrectReferenceText,
                Note = e.Note,
                CreatedAt = DateTimeOffset.UtcNow,
            }).ToList();
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

        private class GeneratedQuestionPayload
        {
            public string Title { get; set; } = string.Empty;

            public string SourceText { get; set; } = string.Empty;

            public JsonElement Brief { get; set; }

            public int? WordCount { get; set; }

            public List<MeaningCheckpointPayload>? MeaningCheckpoints { get; set; }

            public string? FlawedTranslationText { get; set; }

            public List<SeededErrorPayload>? SeededErrors { get; set; }
        }

        private class MeaningCheckpointPayload
        {
            public string CheckpointText { get; set; } = string.Empty;

            public string? CheckpointType { get; set; }

            [JsonConverter(typeof(JsonStringEnumConverter))]
            public CheckpointImportance Importance { get; set; }
        }

        private class SeededErrorPayload
        {
            public int PositionStart { get; set; }

            public int PositionEnd { get; set; }

            public string ErrorCategory { get; set; } = string.Empty;

            public string CorrectReferenceText { get; set; } = string.Empty;

            public string? Note { get; set; }
        }
    }
}
