using System.Text.Json;
using System.Text.Json.Serialization;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;
using DeepLearning.Domain.Events;
using DeepLearning.Domain.Exceptions;
using MediatR;

namespace DeepLearning.Application.Features.Submissions.Commands.GradeSubmission
{
    /// <summary>
    /// Orchestrates one AI grading call end to end: Submitted/GradingFailed -> Grading, build
    /// the prompt via IExamConfigLoader (shared_methodology grading rows + the exam_specific
    /// task-type row, all seeded — see seed_naati_ct_en_zh.sql and
    /// add_grading_content_prompt_template.sql), call the active LLM, validate the response's
    /// structured output against the same assessment_dimensions/error_taxonomies rows used to
    /// build the prompt (design doc §10.3's hard-constraint philosophy), persist GradingResult +
    /// ErrorListItem rows, then Grading -> Graded. Isolation per design doc §10.2: only
    /// meaning_checkpoints + source text + submission content + rubric are read — reference_translations
    /// is never touched here.
    /// </summary>
    public class GradeSubmissionCommandHandler : IRequestHandler<GradeSubmissionCommand, GradeSubmissionResult>
    {
        private static readonly JsonSerializerOptions PayloadJsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly IExamTypeRepository _examTypeRepository;
        private readonly ISubmissionRepository _submissionRepository;
        private readonly IQuestionRepository _questionRepository;
        private readonly IAssessmentDimensionRepository _assessmentDimensionRepository;
        private readonly IErrorTaxonomyRepository _errorTaxonomyRepository;
        private readonly IAiCallLogRepository _aiCallLogRepository;
        private readonly IExamConfigLoader _examConfigLoader;
        private readonly ILlmClientResolver _llmClientResolver;
        private readonly IAiCallRetryExecutor _aiCallRetryExecutor;
        private readonly IUnitOfWork _unitOfWork;
        private readonly Dictionary<ScaleType, IGradingResultInterpreter> _interpreters;

        public GradeSubmissionCommandHandler(
            IExamTypeRepository examTypeRepository,
            ISubmissionRepository submissionRepository,
            IQuestionRepository questionRepository,
            IAssessmentDimensionRepository assessmentDimensionRepository,
            IErrorTaxonomyRepository errorTaxonomyRepository,
            IAiCallLogRepository aiCallLogRepository,
            IExamConfigLoader examConfigLoader,
            ILlmClientResolver llmClientResolver,
            IAiCallRetryExecutor aiCallRetryExecutor,
            IUnitOfWork unitOfWork,
            IEnumerable<IGradingResultInterpreter> interpreters)
        {
            _examTypeRepository = examTypeRepository;
            _submissionRepository = submissionRepository;
            _questionRepository = questionRepository;
            _assessmentDimensionRepository = assessmentDimensionRepository;
            _errorTaxonomyRepository = errorTaxonomyRepository;
            _aiCallLogRepository = aiCallLogRepository;
            _examConfigLoader = examConfigLoader;
            _llmClientResolver = llmClientResolver;
            _aiCallRetryExecutor = aiCallRetryExecutor;
            _unitOfWork = unitOfWork;
            _interpreters = interpreters.ToDictionary(x => x.ScaleType);
        }

        public async Task<GradeSubmissionResult> Handle(GradeSubmissionCommand request, CancellationToken cancellationToken)
        {
            _ = await _examTypeRepository.GetByIdAsync(request.ExamTypeId, cancellationToken)
                ?? throw new NotFoundException(nameof(ExamType), request.ExamTypeId);

            var submission = await _submissionRepository.GetByIdAsync(request.SubmissionId, cancellationToken)
                ?? throw new NotFoundException(nameof(Submission), request.SubmissionId);

            var question = await _questionRepository.GetByIdAsync(submission.QuestionId, cancellationToken)
                ?? throw new NotFoundException(nameof(Question), submission.QuestionId);

            // Grading -> only legal from Submitted (first attempt) or GradingFailed (retry) —
            // Submission.TransitionTo throws InvalidSubmissionStateException otherwise, which
            // rejects a SEQUENTIAL second call arriving after the first already committed
            // Grading. That alone doesn't stop two calls that both read Submitted before either
            // commits — SubmissionConfiguration.UseXminAsConcurrencyToken() closes that race:
            // IUnitOfWork.SaveChangesAsync below throws a ConflictException (409) instead of
            // silently letting both through, translated from EF's DbUpdateConcurrencyException by
            // UnitOfWork itself (Application can't reference EF Core directly).
            submission.TransitionTo(SubmissionStatus.grading);

            var aiCallLog = new AiCallLog
            {
                Id = Guid.NewGuid(),
                RequestType = AiOperationType.grading,
                RelatedId = submission.Id,
                Status = CallStatus.calling,
                AttemptCount = 1,
                MaxRetries = 3,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await _aiCallLogRepository.AddAsync(aiCallLog, cancellationToken);
            // Persisted up front (submission's Grading status included) so both survive even if
            // the LLM call below never returns.
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var checkpoints = await _questionRepository.GetMeaningCheckpointsAsync(submission.QuestionId, cancellationToken);
            var seededErrors = submission.TaskType == TaskType.B
                ? await _questionRepository.GetSeededErrorsAsync(submission.QuestionId, cancellationToken)
                : [];
            var dimensions = await _assessmentDimensionRepository.ListByExamTypeAsync(request.ExamTypeId, submission.TaskType, cancellationToken);
            var errorTaxonomies = await _errorTaxonomyRepository.ListByExamTypeAsync(request.ExamTypeId, cancellationToken);

            var templateModel = BuildTemplateModel(submission, question, checkpoints, seededErrors, dimensions, errorTaxonomies);
            var prompt = await _examConfigLoader.BuildPromptAsync(request.ExamTypeId, AiOperationType.grading, templateModel, cancellationToken);

            GradingPayload payload;
            try
            {
                // Design doc §4.2's retry sub-state-machine: re-prompts (same prompt, fresh call)
                // up to aiCallLog.MaxRetries times when the AI's response fails structured-output
                // validation — distinct from Polly's transport-level retries inside CompleteAsync
                // itself, which already ran and gave up before this ever throws. Only the
                // "get a valid payload" step retries — a persistence failure below isn't something
                // re-prompting the AI would fix.
                payload = await _aiCallRetryExecutor.ExecuteAsync(aiCallLog, async () =>
                {
                    var llmClient = await _llmClientResolver.GetActiveClientAsync(cancellationToken);
                    var completion = await llmClient.CompleteAsync(
                        new LlmCompletionRequest(SystemPrompt: null, UserPrompt: prompt, MaxTokens: 8192),
                        cancellationToken);
                    aiCallLog.LatencyMs = completion.LatencyMs;

                    var parsed = ParsePayload(completion.Text);
                    ValidatePayload(parsed, dimensions, errorTaxonomies);
                    return parsed;
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                await FailAsync(submission, aiCallLog, $"Failed after {aiCallLog.AttemptCount} attempt(s): {ex.Message}", cancellationToken);
                throw new AiCallFailedException($"Grading response could not be used: {ex.Message}", ex);
            }

            // Entity construction + the final persist is its own try/catch so that ANY failure
            // (including a SaveChangesAsync that trips a DB constraint the checks above didn't
            // anticipate) still transitions the submission to GradingFailed. Without this, a
            // failure here would leave the submission stuck in Grading forever — the state
            // machine has no Grading->Grading transition, so a stuck submission could never be
            // retried.
            try
            {
                var dimensionsByKey = dimensions.ToDictionary(x => x.DimensionKey);
                var taxonomiesByKey = errorTaxonomies.ToDictionary(x => x.CategoryKey);

                var gradingResults = payload.Dimensions.Select(d =>
                {
                    var dimension = dimensionsByKey[d.DimensionKey];
                    var interpretation = _interpreters[dimension.ScaleType].Interpret(d.Band.ToString(), dimension.PassThreshold);

                    return new GradingResult
                    {
                        Id = Guid.NewGuid(),
                        SubmissionId = submission.Id,
                        DimensionId = dimension.Id,
                        RubricVersion = dimension.RubricVersion,
                        Band = interpretation.Band,
                        PassBool = interpretation.PassBool,
                        Rationale = d.Rationale,
                        CumulativeDensityFlag = d.CumulativeDensityFlag,
                        CumulativeDensityNote = d.CumulativeDensityNote,
                        EstimatedPassProbability = d.EstimatedPassProbability,
                        CreatedAt = DateTimeOffset.UtcNow,
                    };
                }).ToList();
                await _submissionRepository.AddGradingResultsAsync(gradingResults, cancellationToken);

                var errorItems = payload.Errors.Select(e => new ErrorListItem
                {
                    Id = Guid.NewGuid(),
                    SubmissionId = submission.Id,
                    PositionRef = e.PositionRef,
                    SourceTextSnippet = e.SourceTextSnippet,
                    UserTextSnippet = e.UserTextSnippet,
                    ErrorTaxonomyId = taxonomiesByKey[e.ErrorCategory].Id,
                    DimensionId = dimensionsByKey[e.DimensionKey].Id,
                    ImpactsCore = e.ImpactsCore,
                    Explanation = e.Explanation,
                    Suggestion = e.Suggestion,
                    CreatedAt = DateTimeOffset.UtcNow,
                }).ToList();
                await _submissionRepository.AddErrorListItemsAsync(errorItems, cancellationToken);

                submission.TransitionTo(SubmissionStatus.graded);
                submission.AddDomainEvent(new SubmissionGradedEvent
                {
                    SubmissionId = submission.Id,
                    UserId = submission.UserId,
                    QuestionId = submission.QuestionId,
                    ExamTypeId = request.ExamTypeId,
                    TaskType = submission.TaskType,
                    GradedAt = DateTimeOffset.UtcNow,
                });

                aiCallLog.Status = CallStatus.success;
                aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return new GradeSubmissionResult(submission.Id, submission.Status, gradingResults.Count, errorItems.Count);
            }
            catch (Exception ex)
            {
                // If TransitionTo(Graded) above already ran in-memory before the failure (e.g.
                // the final SaveChangesAsync call itself is what threw), nothing from this
                // try block was actually committed — one SaveChangesAsync call is one DB
                // transaction, so the DB is still sitting at Grading. Reset the in-memory status
                // to match before handing off to FailAsync, otherwise FailAsync's own
                // TransitionTo(GradingFailed) would illegally see Graded as the "current" status
                // and throw a second, more confusing exception on top of the real one.
                if (submission.Status == SubmissionStatus.graded)
                {
                    submission.Status = SubmissionStatus.grading;
                }

                await FailAsync(submission, aiCallLog, $"Failed to persist grading result: {ex.Message}", cancellationToken);
                throw new AiCallFailedException($"Grading response could not be used: {ex.Message}", ex);
            }
        }

        private async Task FailAsync(Submission submission, AiCallLog aiCallLog, string errorMessage, CancellationToken cancellationToken)
        {
            submission.TransitionTo(SubmissionStatus.grading_failed);
            aiCallLog.Status = CallStatus.final_failure;
            aiCallLog.LastErrorMessage = errorMessage;
            aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        private static object BuildTemplateModel(
            Submission submission,
            Question question,
            List<MeaningCheckpoint> checkpoints,
            List<TaskBSeededError> seededErrors,
            List<AssessmentDimension> dimensions,
            List<ErrorTaxonomy> errorTaxonomies) => new
            {
                TaskType = submission.TaskType.ToString(),
                // The source article's own title. Without it the AI sees only the body and
                // reads the user's translated title as invented ("无中生有的信息增添"),
                // penalising a faithful rendering of a title the source actually had. Empty
                // when the source genuinely has no title — the template branches on that.
                SourceTitle = question.Title,
                SourceText = question.SourceText,
                // TaskB only (null for TaskA) — TaskBSeededError.PositionStart/PositionEnd are
                // character offsets into this text, so without it the AI has no way to check
                // whether the user's annotated positions/corrections are actually right.
                FlawedTranslationText = question.FlawedTranslationText,
                SubmissionContent = submission.Content,
                MeaningCheckpoints = checkpoints.Select(c => new
                {
                    CheckpointText = c.CheckpointText,
                    Importance = c.Importance.ToString(),
                }),
                SeededErrors = seededErrors.Select(e => new
                {
                    PositionStart = e.PositionStart,
                    PositionEnd = e.PositionEnd,
                    ErrorCategory = e.ErrorTaxonomy!.CategoryKey,
                    CorrectReferenceText = e.CorrectReferenceText,
                }),
                Dimensions = dimensions.Select(d => new
                {
                    DimensionKey = d.DimensionKey,
                    DimensionName = d.DimensionName,
                    PassThreshold = d.PassThreshold,
                    LevelDescriptions = JsonSerializer.Deserialize<Dictionary<string, string>>(d.LevelDescriptions) ?? [],
                }),
                ErrorTaxonomies = errorTaxonomies.Select(t => new
                {
                    CategoryKey = t.CategoryKey,
                    CategoryName = t.CategoryName,
                    Description = t.Description,
                }),
            };

        private static GradingPayload ParsePayload(string rawText)
        {
            var json = StripMarkdownFence(rawText.Trim());
            return JsonSerializer.Deserialize<GradingPayload>(json, PayloadJsonOptions)
                ?? throw new InvalidOperationException("Deserialized to null.");
        }

        /// <summary>
        /// Design doc §10.3: error_category (and, by the same logic, dimension_key) is a hard
        /// constraint checked in code against error_taxonomies/assessment_dimensions, not just a
        /// prompt reminder — an AI response referencing a category/dimension we don't have on
        /// file is rejected outright rather than persisted.
        /// </summary>
        private static void ValidatePayload(GradingPayload payload, List<AssessmentDimension> dimensions, List<ErrorTaxonomy> errorTaxonomies)
        {
            var dimensionKeys = dimensions.Select(x => x.DimensionKey).ToHashSet();
            var taxonomyKeys = errorTaxonomies.Select(x => x.CategoryKey).ToHashSet();

            foreach (var dimension in payload.Dimensions)
            {
                if (!dimensionKeys.Contains(dimension.DimensionKey))
                {
                    throw new RubricVersionNotFoundException(dimension.DimensionKey);
                }

                // grading_results.band has a DB CHECK constraint (1-5) regardless of scale_type —
                // catch an out-of-range value here (a clean, already-handled rejection) rather
                // than letting it surface as a raw DbUpdateException from the final SaveChangesAsync.
                if (dimension.Band is < 1 or > 5)
                {
                    throw new InvalidOperationException(
                        $"band {dimension.Band} for dimension '{dimension.DimensionKey}' is outside the valid 1-5 range.");
                }
            }

            foreach (var error in payload.Errors)
            {
                if (!taxonomyKeys.Contains(error.ErrorCategory))
                {
                    throw new InvalidOperationException($"error_category '{error.ErrorCategory}' is not a known error taxonomy for this exam type.");
                }

                if (!dimensionKeys.Contains(error.DimensionKey))
                {
                    throw new RubricVersionNotFoundException(error.DimensionKey);
                }
            }
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

        private class GradingPayload
        {
            public List<DimensionPayload> Dimensions { get; set; } = [];

            public List<ErrorPayload> Errors { get; set; } = [];
        }

        private class DimensionPayload
        {
            public string DimensionKey { get; set; } = string.Empty;

            public int Band { get; set; }

            public string Rationale { get; set; } = string.Empty;

            public bool CumulativeDensityFlag { get; set; }

            public string? CumulativeDensityNote { get; set; }

            public decimal? EstimatedPassProbability { get; set; }
        }

        private class ErrorPayload
        {
            public string? PositionRef { get; set; }

            public string? SourceTextSnippet { get; set; }

            public string? UserTextSnippet { get; set; }

            [JsonPropertyName("errorCategory")]
            public string ErrorCategory { get; set; } = string.Empty;

            public string DimensionKey { get; set; } = string.Empty;

            public bool ImpactsCore { get; set; }

            public string? Explanation { get; set; }

            public string? Suggestion { get; set; }
        }
    }
}
