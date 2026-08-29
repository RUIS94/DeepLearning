using System.Text.Json;
using System.Text.Json.Serialization;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Exceptions;
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
        private readonly IGenerationPolicyRepository _generationPolicyRepository;
        private readonly IAiCallLogRepository _aiCallLogRepository;
        private readonly IExamConfigLoader _examConfigLoader;
        private readonly ILlmClientResolver _llmClientResolver;
        private readonly IUnitOfWork _unitOfWork;

        public GenerateQuestionCommandHandler(
            IExamTypeRepository examTypeRepository,
            IUserRepository userRepository,
            IQuestionRepository questionRepository,
            IErrorTaxonomyRepository errorTaxonomyRepository,
            IGenerationPolicyRepository generationPolicyRepository,
            IAiCallLogRepository aiCallLogRepository,
            IExamConfigLoader examConfigLoader,
            ILlmClientResolver llmClientResolver,
            IUnitOfWork unitOfWork)
        {
            _examTypeRepository = examTypeRepository;
            _userRepository = userRepository;
            _questionRepository = questionRepository;
            _errorTaxonomyRepository = errorTaxonomyRepository;
            _generationPolicyRepository = generationPolicyRepository;
            _aiCallLogRepository = aiCallLogRepository;
            _examConfigLoader = examConfigLoader;
            _llmClientResolver = llmClientResolver;
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

            var difficulty = request.Difficulty ?? await ResolveDifficultyAsync(request.ExamTypeId, cancellationToken);
            var errorTaxonomies = await _errorTaxonomyRepository.ListByExamTypeAsync(request.ExamTypeId, cancellationToken);

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

            LlmCompletionResult completion;
            try
            {
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
                };
                var prompt = await _examConfigLoader.BuildPromptAsync(
                    request.ExamTypeId, AiOperationType.question_gen, templateModel, cancellationToken);

                var llmClient = await _llmClientResolver.GetActiveClientAsync(cancellationToken);
                completion = await llmClient.CompleteAsync(
                    new LlmCompletionRequest(SystemPrompt: null, UserPrompt: prompt, MaxTokens: 4096),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                await FailAsync(aiCallLog, ex.Message, cancellationToken);
                throw;
            }

            GeneratedQuestionPayload payload;
            List<TaskBSeededError> seededErrors;
            try
            {
                payload = ParsePayload(completion.Text);

                seededErrors = request.TaskType == TaskType.B
                    ? ValidateAndBuildTaskBSeededErrors(payload, errorTaxonomies)
                    : [];
            }
            catch (Exception ex)
            {
                await FailAsync(aiCallLog, $"Failed to parse/validate LLM response as the expected JSON shape: {ex.Message}", cancellationToken);
                throw new AiCallFailedException($"Claude response could not be used: {ex.Message}", ex);
            }

            var question = new Question
            {
                Id = Guid.NewGuid(),
                TaskType = request.TaskType,
                Difficulty = difficulty,
                Title = payload.Title,
                Brief = payload.Brief.ValueKind == JsonValueKind.Undefined ? null : payload.Brief.GetRawText(),
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

            aiCallLog.Status = CallStatus.success;
            aiCallLog.RelatedId = question.Id;
            aiCallLog.LatencyMs = completion.LatencyMs;
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
