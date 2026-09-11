using System.Text.Json;
using System.Text.Json.Serialization;
using DeepLearning.Application.Interfaces;
using DeepLearning.Domain.Entities;
using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Features.FollowUpThreads
{
    /// <summary>
    /// Shared plumbing for the four FollowUpThreads handlers (Create/AddMessage/Close/Get) —
    /// factored out because unlike the old single-shot CreateFollowUpQuestionCommandHandler,
    /// three separate handlers now need the identical "load grading context, build the prompt
    /// model, parse+validate the AI's structured JSON" sequence. See FollowUpThread's own doc
    /// comment for why per-round replies (FollowUpTurnPayload) and the closing summary call
    /// (FollowUpSummaryPayload) are different contracts.
    /// </summary>
    internal static class FollowUpThreadSupport
    {
        public static async Task<FollowUpThreadContext> LoadContextAsync(
            Guid examTypeId,
            Submission submission,
            Question question,
            IAssessmentDimensionRepository assessmentDimensionRepository,
            IErrorTaxonomyRepository errorTaxonomyRepository,
            ISubmissionRepository submissionRepository,
            IReferenceTranslationRepository referenceTranslationRepository,
            CancellationToken cancellationToken)
        {
            var dimensions = await assessmentDimensionRepository.ListByExamTypeAsync(examTypeId, submission.TaskType, cancellationToken);
            var errorTaxonomies = await errorTaxonomyRepository.ListByExamTypeAsync(examTypeId, cancellationToken);
            var gradingResults = await submissionRepository.GetGradingResultsAsync(submission.Id, cancellationToken);
            var errorList = await submissionRepository.GetErrorListAsync(submission.Id, cancellationToken);
            var referenceTranslation = await referenceTranslationRepository.GetByQuestionIdAsync(question.Id, cancellationToken);

            return new FollowUpThreadContext(dimensions, errorTaxonomies, gradingResults, errorList, referenceTranslation);
        }

        /// <summary>
        /// questionText is the newest user question ("" for the closing summary call, which has
        /// no single distinguished question — see add_followup_thread_prompt_templates.sql,
        /// the followup_summary template only renders `history`, never `question_text`).
        /// history is prior turns only for a per-round call (the newest question is passed
        /// separately via questionText) but ALL turns for the closing summary call.
        ///
        /// Dimensions carry level_descriptions (the official per-Band rubric text, same
        /// Dictionary&lt;string,string&gt; shape GradeSubmissionCommandHandler feeds the grading
        /// template) and pass_threshold, so the followup templates' "对照 Band 原文核对,不要凭印象"
        /// instruction actually has the Band text to check against; error taxonomies carry
        /// Description for the same reason. History AI turns carry that round's Verdict so the
        /// model reads the recorded adjudications instead of re-inferring them from prose
        /// (reset_followup_prompts_v1_production.sql).
        ///
        /// kind is the thread's FollowUpThreadKind as a string ("knowledge" / "dispute" /
        /// "score_challenge") — the followup template gates the whole evaluation scaffold
        /// (error list, grading results, Band text, taxonomy, Major/Minor definitions) behind
        /// {{ if kind != "knowledge" }}, so a plain knowledge question isn't buried under it.
        /// major/minor_error_definition are the canonical NAATI wording from
        /// ErrorSeverityDefinitions, always supplied (the template decides whether to show them).
        /// challengedDimensionId (score_challenge only) resolves to challenged_dimension_key so
        /// the template can name the exact dimension whose Band is in dispute.
        /// </summary>
        public static object BuildTemplateModel(
            string kind,
            string questionText,
            string? contextRef,
            Submission submission,
            Question question,
            FollowUpThreadContext context,
            IEnumerable<FollowUpMessage> history,
            Guid? challengedDimensionId = null) => new
            {
                Kind = kind,
                MajorErrorDefinition = ErrorSeverityDefinitions.Major,
                MinorErrorDefinition = ErrorSeverityDefinitions.Minor,
                ChallengedDimensionKey = challengedDimensionId is { } cdId
                    ? context.Dimensions.FirstOrDefault(d => d.Id == cdId)?.DimensionKey
                    : null,
                QuestionText = questionText,
                ContextRef = contextRef,
                TaskType = submission.TaskType.ToString(),
                SourceText = question.SourceText,
                SubmissionContent = submission.Content,
                GradingResults = context.GradingResults.Select(r => new
                {
                    DimensionKey = r.Dimension!.DimensionKey,
                    Band = r.Band,
                    Rationale = r.Rationale,
                    CumulativeDensityNote = r.CumulativeDensityNote,
                }),
                Errors = context.ErrorList.Select(e => new
                {
                    PositionRef = e.PositionRef,
                    DimensionKey = e.Dimension!.DimensionKey,
                    ErrorCategory = e.ErrorTaxonomy!.CategoryKey,
                    Severity = e.Severity.ToString(),
                    Summary = e.Summary,
                    Explanation = e.Explanation,
                    ImpactsCore = e.ImpactsCore,
                }),
                Dimensions = context.Dimensions.Select(d => new
                {
                    DimensionKey = d.DimensionKey,
                    DimensionName = d.DimensionName,
                    PassThreshold = d.PassThreshold,
                    LevelDescriptions = JsonSerializer.Deserialize<Dictionary<string, string>>(d.LevelDescriptions) ?? [],
                }),
                ErrorTaxonomies = context.ErrorTaxonomies.Select(t => new
                {
                    CategoryKey = t.CategoryKey,
                    CategoryName = t.CategoryName,
                    Description = t.Description,
                }),
                ReferenceTranslation = context.ReferenceTranslation is null ? null : new
                {
                    ReferenceText = context.ReferenceTranslation.ReferenceText,
                    ComparisonNotes = context.ReferenceTranslation.ComparisonNotes,
                },
                History = history.Select(m => new
                {
                    Role = m.Role.ToString(),
                    Content = m.Content,
                    Verdict = m.Verdict?.ToString(),
                }),
            };

        public static HashSet<string> DimensionKeys(FollowUpThreadContext context)
            => context.Dimensions.Select(x => x.DimensionKey).ToHashSet();

        /// <summary>
        /// A per-round reply's verdict is optional now (design revision, 2026-09-02): a follow-up
        /// thread isn't only for disputing a judgment — it's also a place to ask about a knowledge
        /// point / phrasing / term, and a purely explanatory turn has no verdict. `pending` from
        /// the model is normalised to null (same "no adjudication this turn" meaning).
        /// </summary>
        public static void NormaliseTurnPayload(FollowUpTurnPayload payload)
        {
            if (payload.Verdict == FollowUpVerdict.pending)
            {
                payload.Verdict = null;
            }
        }

        /// <summary>
        /// Same validation CreateFollowUpQuestionCommandHandler used to apply per-round — see that
        /// class's history for the original rationale — but finalVerdict is now optional: a thread
        /// that never actually disputed a judgment closes with finalVerdict = null and no
        /// standardRevision. Only finalVerdict == user_correct still requires (and gets) one.
        /// </summary>
        public static void ValidateSummaryPayload(FollowUpSummaryPayload payload, HashSet<string> dimensionKeys)
        {
            if (payload.FinalVerdict == FollowUpVerdict.pending)
            {
                payload.FinalVerdict = null;
            }

            if (payload.FinalVerdict != FollowUpVerdict.user_correct)
            {
                return;
            }

            var revision = payload.StandardRevision
                ?? throw new InvalidOperationException("finalVerdict=user_correct requires a standardRevision object.");

            if (string.IsNullOrWhiteSpace(revision.DimensionOrRule))
            {
                throw new InvalidOperationException("standardRevision.dimensionOrRule must not be empty.");
            }

            if (revision.Scope == OverrideScope.grading_rubric && !dimensionKeys.Contains(revision.DimensionOrRule))
            {
                throw new InvalidOperationException(
                    $"standardRevision.dimensionOrRule '{revision.DimensionOrRule}' is not a known assessment dimension for this exam type.");
            }

            if (string.IsNullOrWhiteSpace(revision.RevisedRuleText))
            {
                throw new InvalidOperationException("standardRevision.revisedRuleText must not be empty.");
            }
        }

        /// <summary>
        /// decision=uphold needs nothing further. decision=adjust must name the new Band (1..5)
        /// and say why — CloseFollowUpThreadCommandHandler rewrites the GradingResult and writes
        /// a GradingResultRevision from exactly these two fields.
        /// </summary>
        public static void ValidateScoreChallengePayload(ScoreChallengeSummaryPayload payload)
        {
            if (payload.Decision != ScoreChallengeDecision.adjust)
            {
                return;
            }

            if (payload.RevisedBand is not (>= 1 and <= 5))
            {
                throw new InvalidOperationException("decision=adjust requires revisedBand in 1..5.");
            }

            if (string.IsNullOrWhiteSpace(payload.RevisedRationale))
            {
                throw new InvalidOperationException("decision=adjust requires a non-empty revisedRationale.");
            }
        }

    }

    internal record FollowUpThreadContext(
        List<AssessmentDimension> Dimensions,
        List<ErrorTaxonomy> ErrorTaxonomies,
        List<GradingResult> GradingResults,
        List<ErrorListItem> ErrorList,
        ReferenceTranslation? ReferenceTranslation);

    /// <summary>Structured-output contract for a per-round AI reply (AiOperationType.followup) — conversational only, no side effects. Verdict is null when the turn wasn't adjudicating a dispute (e.g. a plain knowledge question).</summary>
    internal class FollowUpTurnPayload
    {
        public string AiResponse { get; set; } = string.Empty;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public FollowUpVerdict? Verdict { get; set; }

        /// <summary>
        /// Only meaningful while the thread is still <see cref="FollowUpThreadKind.knowledge"/>:
        /// the model's read on whether this round was actually challenging a specific finding
        /// (so the user never anchored one / forgot to). True promotes the thread to
        /// <see cref="FollowUpThreadKind.dispute"/> so later rounds carry the evaluation
        /// scaffold. Ignored once the thread is already dispute/score_challenge.
        /// </summary>
        public bool DisputeDetected { get; set; }
    }

    /// <summary>Structured-output contract for the closing summary call (AiOperationType.followup_summary) — this is the one call whose verdict/standardRevision has real side effects. FinalVerdict is null when the thread never disputed a judgment.</summary>
    internal class FollowUpSummaryPayload
    {
        public string AiResponse { get; set; } = string.Empty;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public FollowUpVerdict? FinalVerdict { get; set; }

        public StandardRevisionPayload? StandardRevision { get; set; }
    }

    internal class StandardRevisionPayload
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public OverrideScope Scope { get; set; }

        public string DimensionOrRule { get; set; } = string.Empty;

        public string? OriginalRuleText { get; set; }

        public string RevisedRuleText { get; set; } = string.Empty;
    }

    public enum ScoreChallengeDecision
    {
        uphold,
        adjust,
    }

    /// <summary>
    /// Structured-output contract for the closing call of a FollowUpThreadKind.score_challenge
    /// thread (AiOperationType.score_challenge_summary). Unlike FollowUpSummaryPayload this
    /// never carries a standardRevision — a score challenge rewrites THIS submission's Band, it
    /// does not patch the rubric. decision=adjust => RevisedBand (1..5) + RevisedRationale are
    /// required (ValidateScoreChallengePayload enforces it) and drive the re-grade.
    /// </summary>
    internal class ScoreChallengeSummaryPayload
    {
        public string AiResponse { get; set; } = string.Empty;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public ScoreChallengeDecision Decision { get; set; }

        public int? RevisedBand { get; set; }

        public string? RevisedRationale { get; set; }
    }
}
