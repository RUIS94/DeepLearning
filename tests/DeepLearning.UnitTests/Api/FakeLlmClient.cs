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
    /// Hands back FakeLlmClient directly, bypassing the real llm_provider_settings DB lookup —
    /// swapped in for ILlmClientResolver the same way FakeLlmClient is swapped in for ILlmClient.
    /// </summary>
    public class FakeLlmClientResolver : ILlmClientResolver
    {
        public Task<ILlmClient> GetActiveClientAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<ILlmClient>(new FakeLlmClient());
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

    public class FakeTaskBGenerationLlmClientResolver : ILlmClientResolver
    {
        public Task<ILlmClient> GetActiveClientAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<ILlmClient>(new FakeTaskBGenerationLlmClient());
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

    public class FakeTaskBGenerationLlmClientResolverWithOutOfBoundsPosition : ILlmClientResolver
    {
        public Task<ILlmClient> GetActiveClientAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<ILlmClient>(new FakeTaskBGenerationLlmClientWithOutOfBoundsPosition());
    }

    /// <summary>
    /// Fixed-JSON stand-in for a grading call — dimension_key/error_category are fixed
    /// constants rather than parameterized, matching FakeLlmClient's own fixed-value
    /// convention; tests seed an AssessmentDimension/ErrorTaxonomy using these same keys so
    /// GradeSubmissionCommandHandler's structured-output validation (design doc §10.3) accepts
    /// them.
    /// </summary>
    public class FakeGradingLlmClient : ILlmClient
    {
        public const string DimensionKey = "meaning_transfer";
        public const string ErrorCategoryKey = "distortion";
        public const string Rationale = "Meets Band 2 per the rubric text.";

        public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
        {
            var json = $$"""
                {
                  "dimensions": [
                    {"dimensionKey": "{{DimensionKey}}", "band": 2, "rationale": "{{Rationale}}", "cumulativeDensityFlag": false, "cumulativeDensityNote": null, "estimatedPassProbability": 80}
                  ],
                  "errors": [
                    {"positionRef": "p1", "sourceTextSnippet": "src snippet", "userTextSnippet": "user snippet", "errorCategory": "{{ErrorCategoryKey}}", "dimensionKey": "{{DimensionKey}}", "impactsCore": false, "explanation": "explanation text", "suggestion": "suggestion text"}
                  ]
                }
                """;

            return Task.FromResult(new LlmCompletionResult(json, 10, 20, "fake-model", 5));
        }
    }

    public class FakeGradingLlmClientResolver : ILlmClientResolver
    {
        public Task<ILlmClient> GetActiveClientAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<ILlmClient>(new FakeGradingLlmClient());
    }

    /// <summary>
    /// Same shape as FakeGradingLlmClient but reports an error_category no seeded
    /// ErrorTaxonomy will ever match — proves GradeSubmissionCommandHandler really rejects an
    /// AI response referencing an unknown category (design doc §10.3's hard constraint) instead
    /// of silently persisting it.
    /// </summary>
    public class FakeGradingLlmClientWithInvalidCategory : ILlmClient
    {
        public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
        {
            var json = $$"""
                {
                  "dimensions": [
                    {"dimensionKey": "{{FakeGradingLlmClient.DimensionKey}}", "band": 2, "rationale": "ok", "cumulativeDensityFlag": false, "cumulativeDensityNote": null, "estimatedPassProbability": 80}
                  ],
                  "errors": [
                    {"positionRef": "p1", "sourceTextSnippet": "src", "userTextSnippet": "usr", "errorCategory": "not-a-real-category", "dimensionKey": "{{FakeGradingLlmClient.DimensionKey}}", "impactsCore": false, "explanation": "x", "suggestion": "y"}
                  ]
                }
                """;

            return Task.FromResult(new LlmCompletionResult(json, 10, 20, "fake-model", 5));
        }
    }

    public class FakeGradingLlmClientResolverWithInvalidCategory : ILlmClientResolver
    {
        public Task<ILlmClient> GetActiveClientAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<ILlmClient>(new FakeGradingLlmClientWithInvalidCategory());
    }

    /// <summary>
    /// Same shape as FakeGradingLlmClient but reports a band outside grading_results' 1-5 CHECK
    /// constraint — proves GradeSubmissionCommandHandler rejects this before ever reaching the
    /// DB (ValidatePayload's band-range check) instead of leaving the submission stuck in
    /// Grading when the constraint violation would otherwise surface from SaveChangesAsync.
    /// </summary>
    public class FakeGradingLlmClientWithOutOfRangeBand : ILlmClient
    {
        public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
        {
            var json = $$"""
                {
                  "dimensions": [
                    {"dimensionKey": "{{FakeGradingLlmClient.DimensionKey}}", "band": 9, "rationale": "ok", "cumulativeDensityFlag": false, "cumulativeDensityNote": null, "estimatedPassProbability": 80}
                  ],
                  "errors": []
                }
                """;

            return Task.FromResult(new LlmCompletionResult(json, 10, 20, "fake-model", 5));
        }
    }

    public class FakeGradingLlmClientResolverWithOutOfRangeBand : ILlmClientResolver
    {
        public Task<ILlmClient> GetActiveClientAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<ILlmClient>(new FakeGradingLlmClientWithOutOfRangeBand());
    }

    /// <summary>
    /// Records the rendered prompt it was called with (CapturedPrompt) while still returning a
    /// valid grading JSON payload — lets a test assert on what GradeSubmissionCommandHandler
    /// actually sent the LLM, e.g. that TaskB's flawed_translation_text made it into the prompt.
    /// </summary>
    public class CapturingGradingLlmClient : ILlmClient
    {
        public string? CapturedPrompt { get; private set; }

        public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
        {
            CapturedPrompt = request.UserPrompt;

            var json = $$"""
                {
                  "dimensions": [
                    {"dimensionKey": "{{FakeGradingLlmClient.DimensionKey}}", "band": 2, "rationale": "ok", "cumulativeDensityFlag": false, "cumulativeDensityNote": null, "estimatedPassProbability": 80}
                  ],
                  "errors": []
                }
                """;

            return Task.FromResult(new LlmCompletionResult(json, 10, 20, "fake-model", 5));
        }
    }

    /// <summary>
    /// Resolves to a caller-supplied ILlmClient instance rather than constructing its own —
    /// needed instead of the other Fake*Resolver classes above whenever the test needs to keep
    /// its own reference to the client afterward (e.g. CapturingGradingLlmClient's CapturedPrompt).
    /// </summary>
    public class FixedLlmClientResolver : ILlmClientResolver
    {
        private readonly ILlmClient _client;

        public FixedLlmClientResolver(ILlmClient client)
        {
            _client = client;
        }

        public Task<ILlmClient> GetActiveClientAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_client);
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

        private readonly string _dimensionKey;
        private readonly string _followUpResponseJson;

        public FakeFollowUpFlowLlmClient(string dimensionKey, string followUpResponseJson)
        {
            _dimensionKey = dimensionKey;
            _followUpResponseJson = followUpResponseJson;
        }

        /// <summary>
        /// The actual prompt sent for the most recent non-grading (i.e. follow-up) call — lets a
        /// test assert on what CreateFollowUpQuestionCommandHandler actually sent the LLM, e.g.
        /// that a Question's reference translation made it into the prompt.
        /// </summary>
        public string? LastFollowUpPrompt { get; private set; }

        public Task<LlmCompletionResult> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default)
        {
            if (request.UserPrompt.Contains(GradingMarker, StringComparison.Ordinal))
            {
                var gradingJson = $$"""
                    {
                      "dimensions": [
                        {"dimensionKey": "{{_dimensionKey}}", "band": 2, "rationale": "ok", "cumulativeDensityFlag": false, "cumulativeDensityNote": null, "estimatedPassProbability": 80}
                      ],
                      "errors": []
                    }
                    """;
                return Task.FromResult(new LlmCompletionResult(gradingJson, 10, 20, "fake-model", 5));
            }

            LastFollowUpPrompt = request.UserPrompt;
            return Task.FromResult(new LlmCompletionResult(_followUpResponseJson, 10, 20, "fake-model", 5));
        }
    }

    public class FakeFollowUpFlowLlmClientResolver : ILlmClientResolver
    {
        private readonly ILlmClient _client;

        public FakeFollowUpFlowLlmClientResolver(string dimensionKey, string followUpResponseJson)
        {
            _client = new FakeFollowUpFlowLlmClient(dimensionKey, followUpResponseJson);
        }

        public Task<ILlmClient> GetActiveClientAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_client);
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

    public class FakeDeepLearningLlmClientResolverWithInvalidPattern : ILlmClientResolver
    {
        public Task<ILlmClient> GetActiveClientAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<ILlmClient>(new FakeDeepLearningLlmClientWithInvalidPattern());
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

    public class FakeLlmClientResolverFailingTwiceThenSucceeding : ILlmClientResolver
    {
        private readonly FakeLlmClientFailingTwiceThenSucceeding _client = new();

        public Task<ILlmClient> GetActiveClientAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<ILlmClient>(_client);
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

    public class FixedQuestionGenLlmClientResolver : ILlmClientResolver
    {
        private readonly ILlmClient _client;

        public FixedQuestionGenLlmClientResolver(ILlmClient client)
        {
            _client = client;
        }

        public Task<ILlmClient> GetActiveClientAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_client);
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

    public class FixedProgressTrendLlmClientResolver : ILlmClientResolver
    {
        private readonly ILlmClient _client;

        public FixedProgressTrendLlmClientResolver(ILlmClient client)
        {
            _client = client;
        }

        public Task<ILlmClient> GetActiveClientAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_client);
    }
}
