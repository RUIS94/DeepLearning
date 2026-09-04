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
    /// Orchestrates one grading run end to end: Submitted/GradingFailed -> Grading, four
    /// sequential LLM calls, persist GradingResult + ErrorListItem rows, then Grading -> Graded.
    ///
    /// <para><b>Why four calls and not one</b> (see rebuild_grading_prompt_v3_four_stage.sql and
    /// rebuild_grading_prompt_v4_comprehension_test.sql for the incident write-ups): a single
    /// call asked one model to find deviations, classify them, severity-rate them, best-fit a
    /// Band and estimate a pass probability at once. Two runs over the same submission disagreed
    /// wildly, and the concrete detection rules ("from A to B", "over + time") reliably
    /// out-competed the abstract official Band text, so the model found errors first and
    /// back-filled a Band to match.</para>
    /// <list type="number">
    ///   <item><b>evidence</b> — bilingual, sentence by sentence, every source sentence
    ///     accounted for. Emits checkpoint verdicts + findings.</item>
    ///   <item><b>proofread</b> — the TRANSLATION ALONE, no source. A Chinese copy-editor pass:
    ///     reading with the source alongside hides Chinese-side errors, because you already know
    ///     what the sentence meant to say.</item>
    ///   <item><b>sweep</b> — bilingual, with the easy-to-miss checklist, this learner's weak
    ///     points and the accumulated correction patches.</item>
    ///   <item><b>verdict</b> — official five-Band descriptions ONLY, over the merged evidence.
    ///     Emits band + rationale + cumulativeDensityFlag + confidence + alternativeBand, and is
    ///     explicitly told not to consider pass thresholds.</item>
    /// </list>
    /// <para>The three collection stages are never shown each other's findings — being handed a
    /// list turns a model into a validator that subtracts rather than a searcher that adds — so
    /// the handler takes their union and keeps the harshest reading of any duplicate. Severity
    /// is never named by a model: each stage answers NAATI's three questions and
    /// <see cref="DeriveSeverity"/> maps them.</para>
    /// <para>Pass probability is not asked of the AI at all — it is derived here (see
    /// <see cref="EstimateDimensionPassProbability"/> / <see cref="CombinePassProbability"/>),
    /// per AGENTS.md #5. Pass/fail itself has always been deterministic
    /// (band vs. pass_threshold via IGradingResultInterpreter).</para>
    /// <para>All four stages render from ONE prompt_templates row, gated on {{ stage }} — so a
    /// rubric edit stays a single PUT /api/v1/prompt-templates/{id} and needs no enum change.</para>
    /// Isolation per design doc §10.2 is unchanged: only meaning_checkpoints + source text +
    /// submission content + rubric are read — reference_translations is never touched here.
    /// </summary>
    public class GradeSubmissionCommandHandler : IRequestHandler<GradeSubmissionCommand, GradeSubmissionResult>
    {
        private static readonly JsonSerializerOptions PayloadJsonOptions = new() { PropertyNameCaseInsensitive = true };

        private const string StageEvidence = "evidence";
        private const string StageProofread = "proofread";
        private const string StageSweep = "sweep";
        private const string StageVerdict = "verdict";

        /// <summary>
        /// Two findings are the same defect only if they are on the same dimension, in the same
        /// error category, AND quote the same span of the translation. Deliberately narrow: the
        /// three collection stages are meant to overlap, and a term that is both the wrong
        /// concept (meaning_transfer) and inconsistent with the rest of the text (textual_norms)
        /// is two real defects, not one counted twice. Over-merging silently destroys evidence;
        /// a surviving near-duplicate only costs the verdict stage a line of reading.
        /// </summary>
        private static string DuplicateKey(Finding f)
            => string.Join("|", f.DimensionKey, f.ErrorCategory, Normalise(f.UserTextSnippet));

        /// <summary>Punctuation- and whitespace-insensitive form of a quoted snippet, so two
        /// stages quoting the same span with different surrounding punctuation still match.</summary>
        private static string Normalise(string? text)
            => new((text ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());

        private readonly IExamTypeRepository _examTypeRepository;
        private readonly ISubmissionRepository _submissionRepository;
        private readonly IQuestionRepository _questionRepository;
        private readonly IAssessmentDimensionRepository _assessmentDimensionRepository;
        private readonly IErrorTaxonomyRepository _errorTaxonomyRepository;
        private readonly IWeakPointRepository _weakPointRepository;
        private readonly IStandardOverrideRepository _standardOverrideRepository;
        private readonly IGradingSummaryRepository _gradingSummaryRepository;
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
            IWeakPointRepository weakPointRepository,
            IStandardOverrideRepository standardOverrideRepository,
            IGradingSummaryRepository gradingSummaryRepository,
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
            _weakPointRepository = weakPointRepository;
            _standardOverrideRepository = standardOverrideRepository;
            _gradingSummaryRepository = gradingSummaryRepository;
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
                // 3 stages share one log row, so the retry budget is per-stage: a content
                // failure in the audit stage must not consume the verdict stage's retries.
                MaxRetries = 3,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            await _aiCallLogRepository.AddAsync(aiCallLog, cancellationToken);
            // Persisted up front (submission's Grading status included) so both survive even if
            // the LLM calls below never return.
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Past this line the request's token is deliberately dropped. Grading is four
            // sequential LLM calls and takes minutes end to end — a real run measured 5m03s —
            // so it routinely outlives the HTTP request that started it (Node's undici, which
            // proxies the frontend's call, gives up at 300s). Honouring that cancellation would
            // abandon a submission already committed to Grading, throw away completions the
            // user has paid for, and — because the state machine has no Grading->Grading
            // transition — leave the row unable to be re-graded ever again.
            //
            // Finishing is strictly better: the result is persisted and the client just re-reads
            // the submission. The work is bounded anyway (each call has its own Polly attempt and
            // total timeouts), so this cannot hang indefinitely.
            var workToken = CancellationToken.None;

            var checkpoints = await _questionRepository.GetMeaningCheckpointsAsync(submission.QuestionId, workToken);
            var seededErrors = submission.TaskType == TaskType.B
                ? await _questionRepository.GetSeededErrorsAsync(submission.QuestionId, workToken)
                : [];
            var dimensions = await _assessmentDimensionRepository.ListByExamTypeAsync(request.ExamTypeId, submission.TaskType, workToken);
            var errorTaxonomies = await _errorTaxonomyRepository.ListByExamTypeAsync(request.ExamTypeId, workToken);

            // Design doc §10.4/§10.6 — the two accumulation loops the grader must apply: this
            // user's still-active weak points and the exam type's active correction patches
            // distilled from past disputes. Both now go into the AUDIT stage only. They used to
            // sit in the same call that chose the Band, and because UpdateWeakPointsOnGraded
            // rewrites PatternSummary after every grading, re-grading the same submission was
            // literally a different prompt — one of the biggest sources of the run-to-run swing
            // this pipeline was built to fix. In the audit stage they can still surface a missed
            // error, but they can no longer reach the Band decision.
            var weakPoints = await _weakPointRepository.ListActiveWithCatalogByUserAsync(
                submission.UserId, IWeakPointRepository.GradingPromptWeakPointLimit, workToken);
            var activeOverrides = await _standardOverrideRepository.ListActiveByExamTypeAsync(request.ExamTypeId, workToken);

            List<Finding> findings;
            List<CheckpointVerdict> checkpointVerdicts;
            VerdictPayload verdict;
            try
            {
                // Three INDEPENDENT collection passes, then one verdict over their union.
                //
                // v2 ran a "review the previous stage's list" pass instead, and it behaved
                // exactly as being handed a list makes a model behave: it added nothing while
                // its own reasoning re-read every sentence and talked itself out of each one,
                // and it downgraded four findings. Recall is what these passes are for, so none
                // of them is shown what the others found — the handler merges instead.
                var stages = new[]
                {
                    (Stage: StageEvidence, Prefix: "E", MaxTokens: 8192, WeakPoints: new List<WeakPoint>(), Overrides: new List<StandardOverride>()),
                    // 4096 was not enough once v4 added q3WrongReading to every finding and the termUsage
                    // table on top: an observed run came back finish_reason=length, which cost a
                    // full re-prompt plus backoff and pushed the whole grading past the client's
                    // 300s ceiling. All three collection stages now share one budget.
                    (Stage: StageProofread, Prefix: "P", MaxTokens: 8192, WeakPoints: new List<WeakPoint>(), Overrides: new List<StandardOverride>()),
                    (Stage: StageSweep, Prefix: "S", MaxTokens: 8192, WeakPoints: weakPoints, Overrides: activeOverrides),
                };

                var collected = new List<Finding>();
                checkpointVerdicts = [];

                foreach (var stage in stages)
                {
                    var payload = await RunStageAsync<CollectionPayload>(
                        aiCallLog,
                        request.ExamTypeId,
                        BuildTemplateModel(
                            stage.Stage, submission, question, checkpoints, seededErrors, dimensions,
                            errorTaxonomies, stage.WeakPoints, stage.Overrides, [], []),
                        stage.MaxTokens,
                        validate: p =>
                        {
                            NormaliseFindingIds(p.Findings, stage.Prefix);
                            NormaliseComprehensionClaims(p.Findings);
                            ValidateFindings(p.Findings, dimensions, errorTaxonomies);
                            if (stage.Stage == StageEvidence)
                            {
                                // The forced per-sentence enumeration: without it the model stops
                                // once it feels it has found enough (observed: seven findings, then
                                // silence, in a text with more than twice that many).
                                ValidateSentenceCoverage(p.Sentences);
                            }
                        },
                        workToken);

                    if (stage.Stage == StageEvidence)
                    {
                        checkpointVerdicts = BuildCheckpointVerdicts(checkpoints, payload.CheckpointVerdicts);
                    }

                    collected.AddRange(payload.Findings);
                }

                findings = MergeCollectedFindings(collected);

                // ---- Verdict: official Band text only, over the merged evidence ----------
                verdict = await RunStageAsync<VerdictPayload>(
                    aiCallLog,
                    request.ExamTypeId,
                    BuildTemplateModel(
                        StageVerdict, submission, question, checkpoints, seededErrors, dimensions,
                        errorTaxonomies, [], [], findings, checkpointVerdicts),
                    // 4096 was not enough: this provider emits its deliberation as ordinary
                    // content (reasoning_tokens is 0), so the budget ran out before the JSON
                    // started and the truncated payload failed to parse. v4 also caps each
                    // rationale at 150 characters and forbids restating the procedure.
                    maxTokens: 8192,
                    validate: payload => ValidateVerdict(payload, dimensions),
                    workToken);
            }
            catch (Exception ex)
            {
                await FailAsync(submission, aiCallLog, $"Failed after {aiCallLog.AttemptCount} attempt(s): {ex.Message}");
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

                var gradingResults = verdict.Dimensions.Select(d =>
                {
                    var dimension = dimensionsByKey[d.DimensionKey];
                    var interpretation = _interpreters[dimension.ScaleType].Interpret(d.Band.ToString(), dimension.PassThreshold);
                    var confidence = NormaliseConfidence(d.Confidence);

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
                        CumulativeDensityNote = string.IsNullOrWhiteSpace(d.CumulativeDensityNote) ? null : d.CumulativeDensityNote,
                        Confidence = confidence,
                        AlternativeBand = d.AlternativeBand,
                        EstimatedPassProbability = EstimateDimensionPassProbability(
                            interpretation.Band, dimension.PassThreshold, confidence, d.CumulativeDensityFlag),
                        CreatedAt = DateTimeOffset.UtcNow,
                    };
                }).ToList();
                await _submissionRepository.AddGradingResultsAsync(gradingResults, workToken);

                var errorItems = findings.Select(f =>
                {
                    var severity = DeriveSeverity(f);
                    return new ErrorListItem
                    {
                        Id = Guid.NewGuid(),
                        SubmissionId = submission.Id,
                        PositionRef = f.PositionRef,
                        SourceTextSnippet = f.SourceTextSnippet,
                        UserTextSnippet = f.UserTextSnippet,
                        ErrorTaxonomyId = taxonomiesByKey[f.ErrorCategory].Id,
                        DimensionId = dimensionsByKey[f.DimensionKey].Id,
                        Severity = severity,
                        Summary = string.IsNullOrWhiteSpace(f.Summary) ? null : f.Summary.Trim(),
                        // Legacy column, no longer AI-driven: an error is "core" iff it's
                        // major/critical — i.e. iff it met NAATI's official Major-error test.
                        ImpactsCore = severity is ErrorSeverity.major or ErrorSeverity.critical,
                        Explanation = f.Explanation,
                        Suggestion = f.Suggestion,
                        CreatedAt = DateTimeOffset.UtcNow,
                    };
                }).ToList();
                await _submissionRepository.AddErrorListItemsAsync(errorItems, workToken);

                // Design doc §11 (c): one holistic row above the per-dimension results, derived
                // deterministically here. Upserted so a re-grade after a GradingFailed retry
                // updates the row instead of tripping its unique index.
                var overallProbability = CombinePassProbability(gradingResults);
                var overallPass = gradingResults.All(r => r.PassBool);
                var densityFlag = gradingResults.Any(r => r.CumulativeDensityFlag);
                var densityNote = string.Join(
                    " ",
                    gradingResults
                        .Where(r => !string.IsNullOrWhiteSpace(r.CumulativeDensityNote))
                        .Select(r => r.CumulativeDensityNote!.Trim()));

                var existingSummary = await _gradingSummaryRepository.GetBySubmissionIdAsync(submission.Id, workToken);
                if (existingSummary is null)
                {
                    await _gradingSummaryRepository.AddAsync(new GradingSummary
                    {
                        Id = Guid.NewGuid(),
                        SubmissionId = submission.Id,
                        OverallPassProbability = overallProbability,
                        OverallPassBool = overallPass,
                        CumulativeDensityFlag = densityFlag,
                        CumulativeDensityNote = string.IsNullOrEmpty(densityNote) ? null : densityNote,
                        CreatedAt = DateTimeOffset.UtcNow,
                    }, workToken);
                }
                else
                {
                    existingSummary.OverallPassProbability = overallProbability;
                    existingSummary.OverallPassBool = overallPass;
                    existingSummary.CumulativeDensityFlag = densityFlag;
                    existingSummary.CumulativeDensityNote = string.IsNullOrEmpty(densityNote) ? null : densityNote;
                    existingSummary.CreatedAt = DateTimeOffset.UtcNow;
                }

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

                await _unitOfWork.SaveChangesAsync(workToken);

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

                await FailAsync(submission, aiCallLog, $"Failed to persist grading result: {ex.Message}");
                throw new AiCallFailedException($"Grading response could not be used: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// One pipeline stage: render this stage's slice of the grading template, call the LLM,
        /// parse and hard-validate. Design doc §4.2's retry sub-state-machine — distinct from
        /// Polly's transport-level retries inside CompleteAsync, which already ran and gave up
        /// before this ever throws. The retry budget is reset per stage so one stage's content
        /// failure doesn't leave a later stage with none.
        ///
        /// <para><b>Each retry re-prompts with the rejection reason attached.</b> It used to
        /// re-send the byte-identical prompt, which at temperature 0 is a guaranteed way to get
        /// the byte-identical bad answer: on 2026-09-04 the proofread stage put a dimension key
        /// into errorCategory and did it again on both retries, so the whole grading — four
        /// calls, five minutes, real tokens — died on a single mislabelled field that the model
        /// would almost certainly have fixed if anyone had told it. Determinism is what we want
        /// from the FIRST attempt and precisely what we must break on a retry.</para>
        /// </summary>
        private async Task<T> RunStageAsync<T>(
            AiCallLog aiCallLog,
            Guid examTypeId,
            object templateModel,
            int maxTokens,
            Action<T> validate,
            CancellationToken cancellationToken)
        {
            var prompt = await _examConfigLoader.BuildPromptAsync(examTypeId, AiOperationType.grading, templateModel, cancellationToken);
            aiCallLog.AttemptCount = 1;
            string? rejectionReason = null;

            return await _aiCallRetryExecutor.ExecuteAsync(aiCallLog, async () =>
            {
                var llmClient = await _llmClientResolver.GetActiveClientAsync(cancellationToken);
                var completion = await llmClient.CompleteAsync(
                    // Temperature 0: grading must be reproducible — the same submission against
                    // the same rubric should not swing bands run to run. (It is necessary, not
                    // sufficient: a provider that supports `seed` can set one via
                    // llm_provider_settings.extra_settings without a code change.)
                    new LlmCompletionRequest(
                        SystemPrompt: null,
                        UserPrompt: prompt + BuildRejectionNotice(rejectionReason),
                        MaxTokens: maxTokens,
                        Temperature: 0m),
                    cancellationToken);
                aiCallLog.LatencyMs = (aiCallLog.LatencyMs ?? 0) + completion.LatencyMs;

                try
                {
                    var parsed = ParsePayload<T>(completion.Text);
                    validate(parsed);
                    return parsed;
                }
                catch (Exception ex)
                {
                    rejectionReason = ex.Message;
                    throw;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// The corrective block appended to a re-prompt. Empty on the first attempt. Names the
        /// two mistakes that actually happen — a key put in the wrong field, and prose wrapped
        /// around the JSON — because a bare error string leaves the model free to "fix" it by
        /// rewriting something else.
        /// </summary>
        private static string BuildRejectionNotice(string? rejectionReason)
            => string.IsNullOrWhiteSpace(rejectionReason)
                ? string.Empty
                : "\n\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n"
                    + "上一次输出已被系统拒绝,请重新输出。\n"
                    + "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n"
                    + $"拒绝原因:{rejectionReason}\n"
                    + "请只修正这一处,其余判断保持不变,然后重新输出【完整】的 JSON。\n"
                    + "两个最常见的原因,请对照检查:\n"
                    + "1. errorCategory 与 dimensionKey 是两个不同的字段,取值来自两份不同的清单。"
                    + "errorCategory 只能取错误类别 category_key(如 distortion、unidiomatic_expression、spelling_error),"
                    + "【绝不能】填维度名(如 textual_norms、language_proficiency、meaning_transfer)。\n"
                    + "2. 输出必须是纯 JSON:没有代码块围栏,没有前言,没有推理过程。\n";

        /// <summary>
        /// Records the failure and releases the submission back to a retryable state.
        ///
        /// <para>Takes NO cancellation token, deliberately — AGENTS.md #13. The original request
        /// being cancelled is very often exactly WHY we are here, and passing that same token to
        /// SaveChangesAsync makes the failure handler throw on its own first DB call. That is not
        /// hypothetical: it happened on 2026-09-04, and because the write never landed the
        /// submission stayed in Grading, which the state machine can never leave. The row was
        /// unrecoverable without hand-written SQL.</para>
        /// </summary>
        private async Task FailAsync(Submission submission, AiCallLog aiCallLog, string errorMessage)
        {
            submission.TransitionTo(SubmissionStatus.grading_failed);
            aiCallLog.Status = CallStatus.final_failure;
            aiCallLog.LastErrorMessage = errorMessage;
            aiCallLog.ResolvedAt = DateTimeOffset.UtcNow;
            await _unitOfWork.SaveChangesAsync(CancellationToken.None);
        }

        private static object BuildTemplateModel(
            string stage,
            Submission submission,
            Question question,
            List<MeaningCheckpoint> checkpoints,
            List<TaskBSeededError> seededErrors,
            List<AssessmentDimension> dimensions,
            List<ErrorTaxonomy> errorTaxonomies,
            List<WeakPoint> weakPoints,
            List<StandardOverride> activeOverrides,
            List<Finding> findings,
            List<CheckpointVerdict> checkpointVerdicts) => new
            {
                // Which slice of the single grading template to render: evidence | audit | verdict.
                Stage = stage,
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
                // Index is computed here rather than left to Scriban's implicit {{ for.index }}
                // so the numbering the evidence stage is shown and the numbering
                // BuildCheckpointVerdicts pairs its answers back onto come from ONE place. They
                // agreed by coincidence before; a filtered or reordered loop in the template
                // would have silently desynchronised them.
                MeaningCheckpoints = checkpoints.Select((c, i) => new
                {
                    Index = i + 1,
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
                // Sweep stage only — empty for the evidence, proofread and verdict stages, so
                // the Band decision can never see them. Name = catalog name / legacy label. Description = the per-learner
                // rolling pattern_summary, falling back to the catalog's generic description only
                // until the first summary is computed. Recurring = resurfaced after being resolved.
                WeakPoints = weakPoints.Select(w => new
                {
                    Name = w.Catalog is not null ? w.Catalog.Name : (w.Category ?? string.Empty),
                    Description = w.PatternSummary ?? (w.Catalog is not null ? w.Catalog.Description : string.Empty),
                    Recurring = w.RecurrenceCount > 0,
                }),
                // Sweep stage only, same reason as WeakPoints — distilled from past disputes,
                // applied on top of (never replacing) the rubric.
                ActiveOverrides = activeOverrides.Select(o => new
                {
                    Scope = o.Scope.ToString(),
                    DimensionOrRule = o.DimensionOrRule,
                    RevisedRuleText = o.RevisedRuleText,
                }),
                // Verdict stage only: the merged, de-duplicated union of the three collection
                // passes. Severity is rendered here from DeriveSeverity rather than carried on
                // the finding, so the verdict stage reads the level the system actually stored
                // and no stage ever gets to name one itself.
                Findings = findings.Select(f => new
                {
                    Id = f.Id,
                    PositionRef = f.PositionRef,
                    SourceTextSnippet = f.SourceTextSnippet,
                    UserTextSnippet = f.UserTextSnippet,
                    ErrorCategory = f.ErrorCategory,
                    DimensionKey = f.DimensionKey,
                    Severity = DeriveSeverity(f).ToString(),
                    Summary = f.Summary,
                    Explanation = f.Explanation,
                    Suggestion = f.Suggestion,
                }),
                CheckpointVerdicts = checkpointVerdicts.Select(v => new
                {
                    Index = v.Index,
                    CheckpointText = v.CheckpointText,
                    Importance = v.Importance,
                    Verdict = v.Verdict,
                    Note = v.Note,
                }),
            };

        /// <summary>
        /// Heuristic P(this dimension passes), derived — never asked of the AI, which stamped one
        /// gut number into every dimension (observed 0.40 across the board, then 0.55 across the
        /// board once a gap table was added, because a table keyed only on band-minus-threshold
        /// cannot distinguish two dimensions sitting at the same gap).
        ///
        /// The gap term is the base rate; confidence and the cumulative-density flag are what
        /// make two dimensions at the same gap differ. Low confidence pulls the number toward
        /// 0.5 (we are saying "a second examiner could land elsewhere", which is exactly what a
        /// probability near 0.5 means), and a density flag raised on a dimension that only just
        /// scraped over its threshold is the classic borderline-fail shape.
        /// </summary>
        public static decimal EstimateDimensionPassProbability(
            int band, string? passThreshold, string? confidence, bool cumulativeDensityFlag)
        {
            var thresholdBand = ExtractLeadingInt(passThreshold);
            if (thresholdBand is not { } threshold)
            {
                return 0.50m;
            }

            // Band 1 is best, so a POSITIVE gap means the judged band is better than the line.
            var gap = threshold - band;
            var p = gap switch
            {
                >= 2 => 0.97m,
                1 => 0.90m,
                0 => 0.62m,
                -1 => 0.18m,
                _ => 0.03m,
            };

            // Pull toward 0.5 in proportion to how unsure the verdict stage said it was.
            var pull = confidence switch
            {
                "low" => 0.35m,
                "medium" => 0.15m,
                _ => 0m,
            };
            p += (0.50m - p) * pull;

            // Sitting exactly on the line with the "taken together" clause already triggered is
            // the shape that most often fails on a second reading.
            if (gap == 0 && cumulativeDensityFlag)
            {
                p -= 0.10m;
            }

            return Math.Round(Math.Clamp(p, 0.02m, 0.98m), 2, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// P(every dimension passes). NOT the plain product the previous version used: the three
        /// dimensions are strongly correlated — a translation that mangles meaning is usually
        /// also the one with clumsy Chinese — so multiplying them compounded three "comfortably
        /// over the line" numbers into a figure that read like a near-certain fail (0.88³ ≈ 0.68,
        /// 0.55³ ≈ 0.17). Blend the independence bound (the product) with the perfect-correlation
        /// bound (the minimum), weighted toward correlation. Clamped to [0,1] and rounded to 4 dp
        /// to fit numeric(5,4). No estimates at all -> 1.0, as before.
        /// </summary>
        public static decimal CombinePassProbability(IEnumerable<GradingResult> gradingResults)
        {
            const decimal CorrelationWeight = 0.70m;

            var estimates = gradingResults
                .Select(r => r.EstimatedPassProbability)
                .Where(p => p.HasValue)
                .Select(p => Math.Clamp(p!.Value, 0m, 1m))
                .ToList();

            if (estimates.Count == 0)
            {
                return 1m;
            }

            var product = estimates.Aggregate(1m, (acc, p) => acc * p);
            var minimum = estimates.Min();
            var blended = (CorrelationWeight * minimum) + ((1m - CorrelationWeight) * product);

            return Math.Round(Math.Clamp(blended, 0m, 1m), 4, MidpointRounding.AwayFromZero);
        }

        private static int? ExtractLeadingInt(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            var digits = text.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray();
            return digits.Length > 0 ? int.Parse(new string(digits)) : null;
        }

        private static string? NormaliseConfidence(string? raw)
        {
            var value = raw?.Trim().ToLowerInvariant();
            return value is "high" or "medium" or "low" ? value : null;
        }

        /// <summary>
        /// Fills in ids the model left blank or duplicated, so downstream keying is safe. Model
        /// output that already has clean, unique ids is left untouched — the audit stage refers
        /// to stage 1's ids by name, so renumbering a well-formed list would break that contract.
        /// </summary>
        private static void NormaliseFindingIds(List<Finding> findings, string prefix)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < findings.Count; i++)
            {
                var id = findings[i].Id?.Trim();
                if (string.IsNullOrEmpty(id) || !seen.Add(id))
                {
                    id = $"{prefix}{i + 1}__{Guid.NewGuid():N}"[..12];
                    seen.Add(id);
                }

                findings[i].Id = id;
            }
        }

        /// <summary>
        /// Design doc §10.3: error_category and dimension_key are hard constraints checked in
        /// code against error_taxonomies/assessment_dimensions, not just a prompt reminder — a
        /// response referencing a category/dimension we don't have on file is rejected outright
        /// rather than persisted. severity is enforced the same way.
        /// </summary>
        private static void ValidateFindings(
            List<Finding> findings, List<AssessmentDimension> dimensions, List<ErrorTaxonomy> errorTaxonomies)
        {
            var dimensionKeys = dimensions.Select(x => x.DimensionKey).ToHashSet();
            var taxonomyKeys = errorTaxonomies.Select(x => x.CategoryKey).ToHashSet();

            foreach (var finding in findings)
            {
                if (!taxonomyKeys.Contains(finding.ErrorCategory))
                {
                    throw new InvalidOperationException(
                        $"error_category '{finding.ErrorCategory}' is not a known error taxonomy for this exam type.");
                }

                if (!dimensionKeys.Contains(finding.DimensionKey))
                {
                    throw new RubricVersionNotFoundException(finding.DimensionKey);
                }

            }
        }

        /// <summary>
        /// Turns a finding's three official answers into a stored severity. A pure function on
        /// purpose (AGENTS.md #1): v2 asked the model to answer the three questions AND then name
        /// the level itself, and it repeatedly wrote "Q1 yes, Q2 no, Q3 no -> minor" when its own
        /// rules made that moderate - because v2 called the official tier "official Minor" and one
        /// of the four output values "minor", and the model collapsed the two names. A naming
        /// collision cannot survive a lookup table.
        ///
        /// The mapping is NAATI's own: an error is officially Major iff it affects intent or
        /// purpose/function (q2) or impacts comprehension (q3); everything else is officially
        /// Minor. The four stored values subdivide those two - critical is a Major whose reach
        /// goes past the sentence it sits in, moderate is a Minor that still cost real
        /// propositional content (q1).
        /// </summary>
        public static ErrorSeverity DeriveSeverity(Finding finding)
        {
            if (finding.Q2 || finding.Q3)
            {
                return finding.Q2 && finding.Q3 && finding.ScopeBeyondSentence
                    ? ErrorSeverity.critical
                    : ErrorSeverity.major;
            }

            return finding.Q1 ? ErrorSeverity.moderate : ErrorSeverity.minor;
        }

        /// <summary>
        /// Demotes any q3 the stage could not substantiate. The prompt states the rule -
        /// "if you cannot write down what the reader would misunderstand, then it does not
        /// impact comprehension and q3 is false" - and this applies it, because q3 alone
        /// promotes an error to officially Major and the models answer it far too readily:
        /// the first v3 run came back 38 major / 2 moderate / 8 minor, with "還尚 is redundant,
        /// it affects fluency" scored major. That is NAATI's Minor, verbatim.
        ///
        /// Demoting rather than rejecting is deliberate. An unnamed wrong reading is not a
        /// malformed payload we cannot interpret - it is the stage answering the prompt's own
        /// fallback, so applying that fallback is implementing the contract, not salvaging
        /// bad data.
        /// </summary>
        public static void NormaliseComprehensionClaims(List<Finding> findings)
        {
            foreach (var finding in findings.Where(f => f.Q3 && string.IsNullOrWhiteSpace(f.Q3WrongReading)))
            {
                finding.Q3 = false;
            }
        }

        /// <summary>
        /// Unions the three collection passes. Duplicates are expected - the passes deliberately
        /// overlap so that a miss by one is caught by another - so a duplicate keeps the harsher
        /// reading rather than the first one seen: if any pass judged that a defect impacts
        /// comprehension, that judgement stands. The explanation is kept from whichever copy
        /// argued the case at greatest length, since that is the one the learner reads. Ids are
        /// renumbered so the verdict stage sees one clean sequence.
        /// </summary>
        public static List<Finding> MergeCollectedFindings(List<Finding> collected)
        {
            var merged = new List<Finding>();
            var byKey = new Dictionary<string, Finding>(StringComparer.Ordinal);

            foreach (var finding in collected)
            {
                // A finding that quotes nothing cannot be matched against anything - keep it as
                // its own entry rather than collapsing every such finding onto a single key.
                if (Normalise(finding.UserTextSnippet).Length == 0)
                {
                    merged.Add(finding);
                    continue;
                }

                var key = DuplicateKey(finding);
                if (!byKey.TryGetValue(key, out var existing))
                {
                    byKey[key] = finding;
                    merged.Add(finding);
                    continue;
                }

                existing.Q1 |= finding.Q1;
                existing.Q2 |= finding.Q2;
                existing.Q3 |= finding.Q3;
                existing.ScopeBeyondSentence |= finding.ScopeBeyondSentence;

                if ((finding.Explanation?.Length ?? 0) > (existing.Explanation?.Length ?? 0))
                {
                    existing.Explanation = finding.Explanation;
                    existing.Suggestion = finding.Suggestion;
                }

                existing.SourceTextSnippet ??= finding.SourceTextSnippet;
            }

            for (var i = 0; i < merged.Count; i++)
            {
                merged[i].Id = $"F{i + 1}";
            }

            return merged;
        }

        /// <summary>
        /// The evidence stage must account for every source sentence, ok or not. Without it the
        /// model stops as soon as it feels it has found enough - observed stopping at seven
        /// findings on a text that had more than twice that many, with no signal that it had
        /// stopped early. Contiguous numbering from 1 is the cheapest way to make "I gave up half
        /// way" impossible to express in a valid payload.
        /// </summary>
        private static void ValidateSentenceCoverage(List<SentenceRow> sentences)
        {
            if (sentences.Count == 0)
            {
                throw new InvalidOperationException("evidence stage returned no per-sentence coverage rows.");
            }

            for (var i = 0; i < sentences.Count; i++)
            {
                if (sentences[i].N != i + 1)
                {
                    throw new InvalidOperationException(
                        $"per-sentence coverage must be numbered contiguously from 1; got {sentences[i].N} at position {i + 1}.");
                }
            }
        }

        private static void ValidateVerdict(VerdictPayload payload, List<AssessmentDimension> dimensions)
        {
            var dimensionKeys = dimensions.Select(x => x.DimensionKey).ToHashSet();
            var seen = new HashSet<string>();

            foreach (var dimension in payload.Dimensions)
            {
                if (!dimensionKeys.Contains(dimension.DimensionKey))
                {
                    throw new RubricVersionNotFoundException(dimension.DimensionKey);
                }

                if (!seen.Add(dimension.DimensionKey))
                {
                    throw new InvalidOperationException($"verdict returned dimension '{dimension.DimensionKey}' twice.");
                }

                // grading_results.band has a DB CHECK constraint (1-5) regardless of scale_type —
                // catch an out-of-range value here (a clean, already-handled rejection) rather
                // than letting it surface as a raw DbUpdateException from the final SaveChangesAsync.
                if (dimension.Band is < 1 or > 5)
                {
                    throw new InvalidOperationException(
                        $"band {dimension.Band} for dimension '{dimension.DimensionKey}' is outside the valid 1-5 range.");
                }

                if (dimension.AlternativeBand is { } alternative && alternative is < 1 or > 5)
                {
                    throw new InvalidOperationException(
                        $"alternativeBand {alternative} for dimension '{dimension.DimensionKey}' is outside the valid 1-5 range.");
                }
            }

            var missing = dimensionKeys.Except(seen).ToList();
            if (missing.Count > 0)
            {
                throw new InvalidOperationException($"verdict is missing dimension(s): {string.Join(", ", missing)}.");
            }
        }

        /// <summary>
        /// Pairs the evidence stage's hit/partial/miss calls back onto the checkpoint rows they
        /// refer to, so stages 2 and 3 see the checkpoint text alongside the verdict. Verdicts
        /// for indexes we don't have (a hallucinated checkpoint number) are dropped rather than
        /// failing the run — they carry no weight in either downstream stage.
        /// </summary>
        private static List<CheckpointVerdict> BuildCheckpointVerdicts(
            List<MeaningCheckpoint> checkpoints, List<CheckpointVerdictPayload> reported)
        {
            var byIndex = reported
                .GroupBy(v => v.Index)
                .ToDictionary(g => g.Key, g => g.First());

            return checkpoints
                .Select((c, i) =>
                {
                    var index = i + 1;
                    byIndex.TryGetValue(index, out var reportedVerdict);
                    return new CheckpointVerdict(
                        index,
                        c.CheckpointText,
                        c.Importance.ToString(),
                        reportedVerdict?.Verdict?.Trim().ToLowerInvariant() ?? "unknown",
                        string.IsNullOrWhiteSpace(reportedVerdict?.Note) ? null : reportedVerdict.Note.Trim());
                })
                .ToList();
        }

        private static T ParsePayload<T>(string rawText)
        {
            var json = StripMarkdownFence(rawText.Trim());
            return JsonSerializer.Deserialize<T>(json, PayloadJsonOptions)
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

        private sealed record CheckpointVerdict(int Index, string CheckpointText, string Importance, string Verdict, string? Note);

        /// <summary>One recorded deviation. Severity is NOT on it - see <see cref="DeriveSeverity"/>.</summary>
        public class Finding
        {
            public string? Id { get; set; }

            public string? PositionRef { get; set; }

            public string? SourceTextSnippet { get; set; }

            public string? UserTextSnippet { get; set; }

            [JsonPropertyName("errorCategory")]
            public string ErrorCategory { get; set; } = string.Empty;

            public string DimensionKey { get; set; } = string.Empty;

            /// <summary>Changed the propositional content (referent, scope, logic, tense/aspect, modality).</summary>
            public bool Q1 { get; set; }

            /// <summary>Changed the intent, or the purpose and function of the passage.</summary>
            public bool Q2 { get; set; }

            /// <summary>
            /// Impacts comprehension of the target text - NAATI's own wording. Not "reads
            /// awkwardly": the official Minor tier explicitly covers everything that "does not
            /// impact on the comprehension".
            /// </summary>
            public bool Q3 { get; set; }

            /// <summary>
            /// What the reader actually ends up believing, when <see cref="Q3"/> is true. The
            /// prompt requires it, and <see cref="NormaliseComprehensionClaims"/> demotes an
            /// unsubstantiated Q3 to false - naming the wrong reading is the whole difference
            /// between a comprehension failure and prose that is merely clumsy.
            /// </summary>
            public string? Q3WrongReading { get; set; }

            /// <summary>Reach extends past the sentence it sits in (e.g. a whole-text term swap).</summary>
            public bool ScopeBeyondSentence { get; set; }

            /// <summary>Terse per-error characterisation.</summary>
            public string Summary { get; set; } = string.Empty;

            public string Explanation { get; set; } = string.Empty;

            public string Suggestion { get; set; } = string.Empty;
        }

        /// <summary>
        /// Shared shape for all three collection stages - only the evidence stage populates
        /// Sentences and CheckpointVerdicts, and only the proofread stage populates TermUsage.
        /// </summary>
        private class CollectionPayload
        {
            public List<SentenceRow> Sentences { get; set; } = [];

            public List<CheckpointVerdictPayload> CheckpointVerdicts { get; set; } = [];

            public List<TermUsageRow> TermUsage { get; set; } = [];

            public List<Finding> Findings { get; set; } = [];
        }

        private class SentenceRow
        {
            public int N { get; set; }

            public string? Head { get; set; }

            /// <summary>ok | deviation.</summary>
            public string? Status { get; set; }
        }

        /// <summary>
        /// The proofread stage's terminology audit. Not persisted - its job is to force the model
        /// to actually walk the text's repeated concepts before answering, and anything it marks
        /// inconsistent has to appear in Findings anyway.
        /// </summary>
        private class TermUsageRow
        {
            public string? Concept { get; set; }

            public List<string> Renderings { get; set; } = [];

            public bool Consistent { get; set; }
        }

        private class CheckpointVerdictPayload
        {
            public int Index { get; set; }

            /// <summary>hit | partial | miss.</summary>
            public string? Verdict { get; set; }

            public string? Note { get; set; }
        }

        private class VerdictPayload
        {
            public List<VerdictDimension> Dimensions { get; set; } = [];
        }

        private class VerdictDimension
        {
            public string DimensionKey { get; set; } = string.Empty;

            public int Band { get; set; }

            public int? AlternativeBand { get; set; }

            /// <summary>high | medium | low — normalised to null when the model sends anything else.</summary>
            public string? Confidence { get; set; }

            public string Rationale { get; set; } = string.Empty;

            public bool CumulativeDensityFlag { get; set; }

            public string? CumulativeDensityNote { get; set; }
        }
    }
}
