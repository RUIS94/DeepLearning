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
        private static readonly JsonSerializerOptions PayloadJsonOptions = new() { PropertyNameCaseInsensitive = true };

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
        /// </summary>
        public static object BuildTemplateModel(
            string questionText,
            string? contextRef,
            Submission submission,
            Question question,
            FollowUpThreadContext context,
            IEnumerable<FollowUpMessage> history) => new
            {
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
                }),
                Errors = context.ErrorList.Select(e => new
                {
                    PositionRef = e.PositionRef,
                    ErrorCategory = e.ErrorTaxonomy!.CategoryKey,
                    Explanation = e.Explanation,
                    ImpactsCore = e.ImpactsCore,
                }),
                Dimensions = context.Dimensions.Select(d => new
                {
                    DimensionKey = d.DimensionKey,
                    DimensionName = d.DimensionName,
                }),
                ErrorTaxonomies = context.ErrorTaxonomies.Select(t => new
                {
                    CategoryKey = t.CategoryKey,
                    CategoryName = t.CategoryName,
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
                }),
            };

        public static HashSet<string> DimensionKeys(FollowUpThreadContext context)
            => context.Dimensions.Select(x => x.DimensionKey).ToHashSet();

        public static T ParsePayload<T>(string rawText)
        {
            var json = StripMarkdownFence(rawText.Trim());
            return JsonSerializer.Deserialize<T>(json, PayloadJsonOptions)
                ?? throw new InvalidOperationException("Deserialized to null.");
        }

        public static void ValidateTurnPayload(FollowUpTurnPayload payload)
        {
            if (payload.Verdict == FollowUpVerdict.pending)
            {
                throw new InvalidOperationException("verdict must not be 'pending' — the AI must decide user_correct/user_incorrect/partial.");
            }
        }

        /// <summary>Same validation CreateFollowUpQuestionCommandHandler used to apply per-round — see that class's history for the original rationale.</summary>
        public static void ValidateSummaryPayload(FollowUpSummaryPayload payload, HashSet<string> dimensionKeys)
        {
            if (payload.FinalVerdict == FollowUpVerdict.pending)
            {
                throw new InvalidOperationException("finalVerdict must not be 'pending' — the AI must decide user_correct/user_incorrect/partial.");
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
    }

    internal record FollowUpThreadContext(
        List<AssessmentDimension> Dimensions,
        List<ErrorTaxonomy> ErrorTaxonomies,
        List<GradingResult> GradingResults,
        List<ErrorListItem> ErrorList,
        ReferenceTranslation? ReferenceTranslation);

    /// <summary>Structured-output contract for a per-round AI reply (AiOperationType.followup) — conversational only, no side effects.</summary>
    internal class FollowUpTurnPayload
    {
        public string AiResponse { get; set; } = string.Empty;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public FollowUpVerdict Verdict { get; set; }
    }

    /// <summary>Structured-output contract for the closing summary call (AiOperationType.followup_summary) — this is the one call whose verdict/standardRevision has real side effects.</summary>
    internal class FollowUpSummaryPayload
    {
        public string AiResponse { get; set; } = string.Empty;

        [JsonConverter(typeof(JsonStringEnumConverter))]
        public FollowUpVerdict FinalVerdict { get; set; }

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
}
