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
        private readonly IAiCallLogRepository _aiCallLogRepository;
        private readonly IExamConfigLoader _examConfigLoader;
        private readonly ILlmClientResolver _llmClientResolver;
        private readonly IUnitOfWork _unitOfWork;

        public GenerateQuestionCommandHandler(
            IExamTypeRepository examTypeRepository,
            IUserRepository userRepository,
            IQuestionRepository questionRepository,
            IAiCallLogRepository aiCallLogRepository,
            IExamConfigLoader examConfigLoader,
            ILlmClientResolver llmClientResolver,
            IUnitOfWork unitOfWork)
        {
            _examTypeRepository = examTypeRepository;
            _userRepository = userRepository;
            _questionRepository = questionRepository;
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
                var templateModel = new { Difficulty = request.Difficulty.ToString(), TaskType = request.TaskType.ToString() };
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
            try
            {
                payload = ParsePayload(completion.Text);
            }
            catch (Exception ex)
            {
                await FailAsync(aiCallLog, $"Failed to parse LLM response as the expected JSON shape: {ex.Message}", cancellationToken);
                throw new AiCallFailedException($"Claude response could not be parsed: {ex.Message}", ex);
            }

            var question = new Question
            {
                Id = Guid.NewGuid(),
                TaskType = request.TaskType,
                Difficulty = request.Difficulty,
                Title = payload.Title,
                Brief = payload.Brief.ValueKind == JsonValueKind.Undefined ? null : payload.Brief.GetRawText(),
                SourceText = payload.SourceText,
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

            aiCallLog.Status = CallStatus.success;
            aiCallLog.RelatedId = question.Id;
            aiCallLog.LatencyMs = completion.LatencyMs;
            aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new GenerateQuestionResult(question.Id, question.TaskType, question.Difficulty, question.Title, question.CreatedAt);
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
        }

        private class MeaningCheckpointPayload
        {
            public string CheckpointText { get; set; } = string.Empty;

            public string? CheckpointType { get; set; }

            [JsonConverter(typeof(JsonStringEnumConverter))]
            public CheckpointImportance Importance { get; set; }
        }
    }
}
