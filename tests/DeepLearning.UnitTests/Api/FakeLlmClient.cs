using DeepLearning.Application.Interfaces;

namespace DeepLearning.UnitTests.Api
{
    /// <summary>
    /// Fixed-JSON stand-in for ILlmClient, swapped in via
    /// ApiWebApplicationFactory.WithWebHostBuilder(...) for tests that need to prove the
    /// GenerateQuestion pipeline (parse -> persist -> respond) without a real LLM call.
    /// </summary>
    public class FakeLlmClient : ILlmClient
    {
        public const string FixedTitle = "Fake Generated Question";
        public const string FixedSourceText = "This is a fake generated source text for testing.";

        public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
        {
            var json = $$"""
                {
                  "title": "{{FixedTitle}}",
                  "sourceText": "{{FixedSourceText}}",
                  "brief": {"domain": "test", "textType": "article"},
                  "wordCount": 42,
                  "meaningCheckpoints": [
                    {"checkpointText": "Must convey the fake fact.", "checkpointType": null, "importance": "core"}
                  ]
                }
                """;

            return Task.FromResult(new LlmCompletionResult(json, 10, 20, "fake-model", 5));
        }
    }

    /// <summary>
    /// Fixed-JSON stand-in for a TaskB question-generation call — includes
    /// flawedTranslationText/seededErrors on top of FakeLlmClient's fields. ErrorCategoryKey
    /// must be seeded as a real ErrorTaxonomy for the exam type under test, same convention as
    /// FakeGradingLlmClient.
    /// </summary>
    public class FakeTaskBGenerationLlmClient : ILlmClient
    {
        public const string ErrorCategoryKey = "distortion";
        public const string FlawedTranslationText = "This sentence has an error in it.";

        public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
        {
            var json = $$"""
                {
                  "title": "{{FakeLlmClient.FixedTitle}}",
                  "sourceText": "{{FakeLlmClient.FixedSourceText}}",
                  "brief": {"domain": "test", "textType": "article"},
                  "wordCount": 42,
                  "meaningCheckpoints": [],
                  "flawedTranslationText": "{{FlawedTranslationText}}",
                  "seededErrors": [
                    {"positionStart": 9, "positionEnd": 17, "errorCategory": "{{ErrorCategoryKey}}", "correctReferenceText": "had", "note": null}
                  ]
                }
                """;

            return Task.FromResult(new LlmCompletionResult(json, 10, 20, "fake-model", 5));
        }
    }

    /// <summary>
    /// Same shape as FakeTaskBGenerationLlmClient but its one seededError's position range
    /// falls outside flawedTranslationText — proves GenerateQuestionCommandHandler rejects a
    /// structurally broken TaskB response instead of persisting a Question whose seeded-error
    /// offsets don't actually fit its own FlawedTranslationText.
    /// </summary>
    public class FakeTaskBGenerationLlmClientWithOutOfBoundsPosition : ILlmClient
    {
        public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
        {
            var json = $$"""
                {
                  "title": "{{FakeLlmClient.FixedTitle}}",
                  "sourceText": "{{FakeLlmClient.FixedSourceText}}",
                  "brief": {"domain": "test", "textType": "article"},
                  "wordCount": 42,
                  "meaningCheckpoints": [],
                  "flawedTranslationText": "{{FakeTaskBGenerationLlmClient.FlawedTranslationText}}",
                  "seededErrors": [
                    {"positionStart": 900, "positionEnd": 950, "errorCategory": "{{FakeTaskBGenerationLlmClient.ErrorCategoryKey}}", "correctReferenceText": "had", "note": null}
                  ]
                }
                """;

            return Task.FromResult(new LlmCompletionResult(json, 10, 20, "fake-model", 5));
        }
    }

    /// <summary>
    /// Grading is four sequential LLM calls that expect two different payload shapes — three
    /// collection stages (evidence, proofread, sweep) and then the verdict. The tests seed a stub
    /// prompt template with no {{ stage }} branch, so every call renders the same text and a fake
    /// cannot tell the stages apart by content.
    ///
    /// Rather than count calls — which breaks the moment a test's follow-up prompt also matches
    /// the grading marker, and which makes every fixture order-dependent — one payload carries
    /// BOTH shapes at once. System.Text.Json ignores properties a target type does not declare,
    /// so the collection stages read sentences/findings and ignore dimensions, and the verdict
    /// stage reads dimensions and ignores the rest. All three collection stages then report the
    /// same single finding, which MergeCollectedFindings de-duplicates back to one, so a test
    /// asserting "one error was persisted" still means what it says.
    /// </summary>
    internal static class FakeGradingPayloads
    {
        /// <summary>
        /// Both questions false is NAATI's Minor: a propositional inaccuracy that leaves intent,
        /// function and comprehension intact.
        /// </summary>
        /// <param name="errorCategoryKey">
        /// Null for a clean run that reports no errors at all. Tests that only need a submission
        /// to reach Graded (the follow-up flow, for one) seed an AssessmentDimension but no
        /// ErrorTaxonomy, and a finding citing a category they never seeded would be rejected by
        /// the very hard constraint those tests are not about.
        /// </param>
        public static string Build(
            string dimensionKey,
            string? errorCategoryKey,
            int band = 2,
            string rationale = "ok") => $$"""
            {
              "sentences": [{"n": 1, "head": "fake source sentence", "status": "{{(errorCategoryKey is null ? "ok" : "deviation")}}"}],
              "checkpointVerdicts": [],
              "termUsage": [],
              "findings": [{{Findings(dimensionKey, errorCategoryKey)}}],
              "dimensions": [
                {"dimensionKey": "{{dimensionKey}}", "band": {{band}}, "alternativeBand": {{band}}, "confidence": "high", "cumulativeDensityFlag": false, "cumulativeDensityNote": null, "rationale": "{{rationale}}"}
              ]
            }
            """;

        private static string Findings(string dimensionKey, string? errorCategoryKey)
            => errorCategoryKey is null
                ? string.Empty
                : $$"""
                    {"id": "E1", "positionRef": "p1", "sourceTextSnippet": "src snippet", "userTextSnippet": "user snippet", "errorCategory": "{{errorCategoryKey}}", "dimensionKey": "{{dimensionKey}}", "q1": false, "q2": false, "q2WrongReading": null, "summary": "fake summary", "explanation": "explanation text", "suggestion": "suggestion text"}
                    """;
    }

    /// <summary>
    /// Stand-in for a whole grading run — dimension_key/error_category are fixed constants rather
    /// than parameterized, matching FakeLlmClient's own fixed-value convention; tests seed an
    /// AssessmentDimension/ErrorTaxonomy using these same keys so GradeSubmissionCommandHandler's
    /// structured-output validation (design doc §10.3) accepts them.
    /// </summary>
    public class FakeGradingLlmClient : ILlmClient
    {
        public const string DimensionKey = "meaning_transfer";
        public const string ErrorCategoryKey = "distortion";
        public const string Rationale = "Meets Band 2 per the rubric text.";

        public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new LlmCompletionResult(
                FakeGradingPayloads.Build(DimensionKey, ErrorCategoryKey, rationale: Rationale), 10, 20, "fake-model", 5));
    }

    /// <summary>
    /// Reports an error_category no seeded ErrorTaxonomy will ever match — proves
    /// GradeSubmissionCommandHandler really rejects an AI response referencing an unknown
    /// category (design doc §10.3's hard constraint) instead of silently persisting it. It never
    /// stops returning the bad category, so the first collection stage exhausts its re-prompts
    /// and the whole grading fails, which is the behaviour under test.
    /// </summary>
    public class FakeGradingLlmClientWithInvalidCategory : ILlmClient
    {
        public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new LlmCompletionResult(
                FakeGradingPayloads.Build(FakeGradingLlmClient.DimensionKey, "not-a-real-category"), 10, 20, "fake-model", 5));
    }

    /// <summary>
    /// Reports a band outside grading_results' 1-5 CHECK constraint — proves the handler rejects
    /// it before ever reaching the DB (ValidateVerdict's band-range check) instead of leaving the
    /// submission stuck in Grading when the constraint violation would otherwise surface from
    /// SaveChangesAsync. The collection stages ignore the dimensions block, so only the verdict
    /// trips on it.
    /// </summary>
    public class FakeGradingLlmClientWithOutOfRangeBand : ILlmClient
    {
        public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new LlmCompletionResult(
                FakeGradingPayloads.Build(FakeGradingLlmClient.DimensionKey, FakeGradingLlmClient.ErrorCategoryKey, band: 9),
                10, 20, "fake-model", 5));
    }

    /// <summary>
    /// Records every rendered prompt it was called with while still driving a valid grading run —
    /// lets a test assert on what GradeSubmissionCommandHandler actually sent the LLM, e.g. that
    /// TaskB's flawed_translation_text made it into the prompt. CapturedPrompt is the first
    /// prompt (the evidence stage); CapturedPrompts holds all four.
    /// </summary>
    public class CapturingGradingLlmClient : ILlmClient
    {
        public List<string> CapturedPrompts { get; } = [];

        public string? CapturedPrompt => CapturedPrompts.FirstOrDefault();

        public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
        {
            CapturedPrompts.Add(request.UserPrompt);
            return Task.FromResult(new LlmCompletionResult(
                FakeGradingPayloads.Build(FakeGradingLlmClient.DimensionKey, FakeGradingLlmClient.ErrorCategoryKey),
                10, 20, "fake-model", 5));
        }
    }

    /// <summary>
    /// A follow-up flow needs both a grading call (to get the submission to Graded first) and a
    /// follow-up call, but ApiWebApplicationFactory only lets one ILlmClientResolver be
    /// registered per test client. This client tells the two apart by a literal marker string
    /// each test's seeded PromptTemplate rows render verbatim (see
    /// FollowUpsControllerTests.SeedGradingAndFollowUpTemplatesAsync) and returns a fixed grading
    /// response (parameterized by dimensionKey, so it validates against whatever AssessmentDimension
    /// that test seeded) for the former, or the caller-supplied JSON (varied per test to control
    /// the verdict) for the latter. dimensionKey is caller-supplied rather than a shared constant
    /// because standard_overrides has no exam_type_id column (design doc §9.4) — it matches purely
    /// on (scope, dimensionOrRule), so tests sharing one literal string would silently pollute each
    /// other's confirmation counts through the ApiCollection's one shared Testcontainers DB.
    /// </summary>
    public class FakeFollowUpFlowLlmClient : ILlmClient
    {
        public const string GradingMarker = "GRADING_MARKER";
        public const string FollowUpMarker = "FOLLOWUP_MARKER";
        public const string SummaryMarker = "FOLLOWUP_SUMMARY_MARKER";

        private readonly string _dimensionKey;
        private readonly string _followUpResponseJson;
        private readonly string? _summaryResponseJson;

        public FakeFollowUpFlowLlmClient(string dimensionKey, string followUpResponseJson, string? summaryResponseJson = null)
        {
            _dimensionKey = dimensionKey;
            _followUpResponseJson = followUpResponseJson;
            _summaryResponseJson = summaryResponseJson;
        }

        /// <summary>
        /// The actual prompt sent for the most recent per-round follow-up call — lets a test
        /// assert on what AddFollowUpMessageCommandHandler / CreateFollowUpThreadCommandHandler
        /// actually sent the LLM, e.g. that a Question's reference translation made it into the
        /// prompt, or that prior rounds were replayed as history.
        /// </summary>
        public string? LastFollowUpPrompt { get; private set; }

        /// <summary>The prompt sent for the most recent closing summary call (AiOperationType.followup_summary).</summary>
        public string? LastSummaryPrompt { get; private set; }

        public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
        {
            if (request.UserPrompt.Contains(GradingMarker, StringComparison.Ordinal))
            {
                // These tests seed a dimension but no error taxonomy — they only need the
                // submission to reach Graded, so the grading reports no errors.
                var gradingJson = FakeGradingPayloads.Build(_dimensionKey, errorCategoryKey: null);
                return Task.FromResult(new LlmCompletionResult(gradingJson, 10, 20, "fake-model", 5));
            }

            if (request.UserPrompt.Contains(SummaryMarker, StringComparison.Ordinal))
            {
                LastSummaryPrompt = request.UserPrompt;
                return Task.FromResult(new LlmCompletionResult(
                    _summaryResponseJson ?? throw new InvalidOperationException("This FakeFollowUpFlowLlmClient was constructed without a summaryResponseJson."),
                    10, 20, "fake-model", 5));
            }

            LastFollowUpPrompt = request.UserPrompt;
            return Task.FromResult(new LlmCompletionResult(_followUpResponseJson, 10, 20, "fake-model", 5));
        }
    }

    /// <summary>
    /// Fixed-JSON stand-in for a deep-learning generation call (design doc §10.2/Step 7) — also
    /// records every prompt it was called with and how many times, so a test can assert both
    /// content-isolation (the prompt only ever contains what GenerateDeepLearningContentCommandHandler
    /// is supposed to send) and idempotency (a second call for an already-generated Question never
    /// reaches this client at all).
    /// </summary>
    public class FakeDeepLearningLlmClient : ILlmClient
    {
        public const string ReferenceText = "这是标准参考译文。";
        public const string PatternName = "非限定性定语从句";
        public const string VocabExpr = "in light of";

        public int CallCount { get; private set; }

        public List<string> CapturedPrompts { get; } = [];

        public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            CapturedPrompts.Add(request.UserPrompt);

            var json = $$"""
                {
                  "referenceText": "{{ReferenceText}}",
                  "comparisonNotes": ["注意不要逐字直译"],
                  "sentencePatterns": [
                    {"patternName": "{{PatternName}}", "exampleSentence": "example sentence", "breakdownSteps": {"主干": "x"}, "variants": null, "domain": null, "scenario": null, "frequencyTag": null}
                  ],
                  "vocabExpressions": [
                    {"englishExpr": "{{VocabExpr}}", "chineseEquiv": "鉴于", "contextNote": null, "category": null, "domain": null, "scenario": null, "frequencyTag": null}
                  ]
                }
                """;

            return Task.FromResult(new LlmCompletionResult(json, 10, 20, "fake-model", 5));
        }
    }

    /// <summary>
    /// Same shape as FakeDeepLearningLlmClient but its one sentencePatterns item has an empty
    /// patternName — proves GenerateDeepLearningContentCommandHandler rejects a structurally
    /// invalid item instead of persisting it (design doc §10.3's hard-constraint philosophy).
    /// </summary>
    public class FakeDeepLearningLlmClientWithInvalidPattern : ILlmClient
    {
        public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
        {
            var json = """
                {
                  "referenceText": "reference text",
                  "comparisonNotes": [],
                  "sentencePatterns": [
                    {"patternName": "", "exampleSentence": "x", "breakdownSteps": null, "variants": null, "domain": null, "scenario": null, "frequencyTag": null}
                  ],
                  "vocabExpressions": []
                }
                """;

            return Task.FromResult(new LlmCompletionResult(json, 10, 20, "fake-model", 5));
        }
    }

    /// <summary>
    /// Self-audit fix (2026-08-30, design doc §4.2's retry sub-state-machine): returns malformed
    /// (unparseable) JSON on its first two calls, then a valid GenerateQuestion response on the
    /// third — proves AiCallRetryExecutor really re-prompts on a structured-output failure instead
    /// of giving up on the first bad response, and that AiCallLog.AttemptCount ends up reflecting
    /// how many calls it actually took.
    /// </summary>
    public class FakeLlmClientFailingTwiceThenSucceeding : ILlmClient
    {
        public int CallCount { get; private set; }

        public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (CallCount < 3)
            {
                return Task.FromResult(new LlmCompletionResult("not valid json at all", 10, 20, "fake-model", 5));
            }

            var json = $$"""
                {
                  "title": "{{FakeLlmClient.FixedTitle}}",
                  "sourceText": "{{FakeLlmClient.FixedSourceText}}",
                  "brief": {"domain": "test", "textType": "article"},
                  "wordCount": 42,
                  "meaningCheckpoints": []
                }
                """;
            return Task.FromResult(new LlmCompletionResult(json, 10, 20, "fake-model", 5));
        }
    }

    /// <summary>
    /// Same fixed-valid-JSON response as FakeLlmClient, but records the prompt it was called
    /// with — lets a test assert on what GenerateQuestionCommandHandler actually sent the LLM
    /// (e.g. whether a weak_point_hint made it into the rendered prompt).
    /// </summary>
    public class CapturingQuestionGenLlmClient : ILlmClient
    {
        public string? CapturedPrompt { get; private set; }

        public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
        {
            CapturedPrompt = request.UserPrompt;

            var json = $$"""
                {
                  "title": "{{FakeLlmClient.FixedTitle}}",
                  "sourceText": "{{FakeLlmClient.FixedSourceText}}",
                  "brief": {"domain": "test", "textType": "article"},
                  "wordCount": 42,
                  "meaningCheckpoints": []
                }
                """;
            return Task.FromResult(new LlmCompletionResult(json, 10, 20, "fake-model", 5));
        }
    }

    /// <summary>
    /// Fixed-JSON stand-in for GenerateProgressTrendSnapshotCommandHandler's AI call (Step 9) —
    /// also records every prompt it was called with, so a test can assert the rendered prompt
    /// actually carries the current week's numbers and prior weeks' history.
    /// </summary>
    public class FakeProgressTrendLlmClient : ILlmClient
    {
        public const string TrendNote = "本周meaning_transfer维度较上周有所提升。";

        public int CallCount { get; private set; }

        public List<string> CapturedPrompts { get; } = [];

        public bool KeyTurningPoint { get; set; }

        public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            CapturedPrompts.Add(request.UserPrompt);

            var json = $$"""
                {
                  "trendNote": "{{TrendNote}}",
                  "keyTurningPoint": {{(KeyTurningPoint ? "true" : "false")}}
                }
                """;

            return Task.FromResult(new LlmCompletionResult(json, 10, 20, "fake-model", 5));
        }
    }

    /// <summary>
    /// Always returns a response missing the required trendNote — proves
    /// GenerateProgressTrendSnapshotCommandHandler's AI-narrative failure path leaves the
    /// already-recomputed numeric snapshot row intact instead of losing it, unlike every other
    /// AI-orchestration handler in this codebase (which fail the whole operation).
    /// </summary>
    public class FakeAlwaysInvalidProgressTrendLlmClient : ILlmClient
    {
        public int CallCount { get; private set; }

        public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            var json = """{ "trendNote": "", "keyTurningPoint": false }""";
            return Task.FromResult(new LlmCompletionResult(json, 10, 20, "fake-model", 5));
        }
    }

}
