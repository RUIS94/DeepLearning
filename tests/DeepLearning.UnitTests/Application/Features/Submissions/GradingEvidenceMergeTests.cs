using DeepLearning.Application.Features.Submissions.Commands.GradeSubmission;
using DeepLearning.Domain.Enums;

namespace DeepLearning.UnitTests.Application.Features.Submissions
{
    /// <summary>
    /// The two code-side guarantees the v3 grading pipeline rests on.
    ///
    /// <para><b>Severity is derived, never named.</b> v2 asked the model to answer NAATI's three
    /// questions and then pick a level itself, and on a real submission it wrote "Q1 yes, Q2 no,
    /// Q3 no → minor" four times running when its own stated rule made that moderate — because
    /// the official tier and one of the four output values were both called "Minor" and the model
    /// collapsed the names. A lookup table cannot make that mistake.</para>
    ///
    /// <para><b>The collection passes are unioned, not reconciled.</b> Three stages look for
    /// evidence independently and are never shown each other's findings, so overlap is the design,
    /// not a defect — and a duplicate must not be allowed to soften what another pass found.</para>
    /// </summary>
    public class GradingEvidenceMergeTests
    {
        private static GradeSubmissionCommandHandler.Finding Finding(
            string id,
            string userSnippet = "晒斑",
            string dimension = "meaning_transfer",
            string category = "distortion",
            bool q1 = false,
            bool q2 = false,
            bool q3 = false,
            bool scope = false,
            string explanation = "说明") => new()
            {
                Id = id,
                PositionRef = "标题",
                SourceTextSnippet = "Sunburn",
                UserTextSnippet = userSnippet,
                ErrorCategory = category,
                DimensionKey = dimension,
                Q1 = q1,
                Q2 = q2,
                Q3 = q3,
                ScopeBeyondSentence = scope,
                Summary = "摘要",
                Explanation = explanation,
                Suggestion = "建议",
            };

        [Theory]
        // q1 alone is NAATI's Minor error: propositional content moved, intent and comprehension
        // intact. This is the exact case v2 kept mislabelling as minor.
        [InlineData(true, false, false, false, ErrorSeverity.moderate)]
        [InlineData(false, false, false, false, ErrorSeverity.minor)]
        // Either q2 or q3 makes it officially Major.
        [InlineData(true, true, false, false, ErrorSeverity.major)]
        [InlineData(true, false, true, false, ErrorSeverity.major)]
        [InlineData(false, false, true, false, ErrorSeverity.major)]
        // critical needs both halves of the official Major test AND reach past the sentence.
        [InlineData(true, true, true, true, ErrorSeverity.critical)]
        [InlineData(true, true, true, false, ErrorSeverity.major)]
        // Scope alone never promotes an officially-Minor error.
        [InlineData(true, false, false, true, ErrorSeverity.moderate)]
        public void Severity_comes_from_the_three_official_answers(
            bool q1, bool q2, bool q3, bool scope, ErrorSeverity expected)
        {
            var severity = GradeSubmissionCommandHandler.DeriveSeverity(
                Finding("E1", q1: q1, q2: q2, q3: q3, scope: scope));

            Assert.Equal(expected, severity);
        }

        [Fact]
        public void A_comprehension_claim_with_no_wrong_reading_named_is_demoted()
        {
            // q3 alone promotes an error to officially Major, and the models answer it far too
            // readily: the first v3 run scored "還尚 is redundant, it affects fluency" as major.
            // The prompt requires the claim to be paid for by naming what the reader ends up
            // believing; this applies the prompt's own fallback when it is not.
            var unpaid = Finding("E1", q1: true, q3: true);
            var paid = Finding("E2", userSnippet: "被晒伤的信号", q1: true, q3: true);
            paid.Q3WrongReading = "读者会以为信号本身被晒伤了";

            var findings = new List<GradeSubmissionCommandHandler.Finding> { unpaid, paid };
            GradeSubmissionCommandHandler.NormaliseComprehensionClaims(findings);

            Assert.False(unpaid.Q3);
            Assert.Equal(ErrorSeverity.moderate, GradeSubmissionCommandHandler.DeriveSeverity(unpaid));
            Assert.True(paid.Q3);
            Assert.Equal(ErrorSeverity.major, GradeSubmissionCommandHandler.DeriveSeverity(paid));
        }

        [Fact]
        public void Demotion_leaves_q1_and_q2_untouched()
        {
            // Only the unsubstantiated q3 is withdrawn — an error that genuinely changed the
            // intent stays Major on q2 alone, with or without a named wrong reading.
            var finding = Finding("E1", q1: true, q2: true, q3: true);

            GradeSubmissionCommandHandler.NormaliseComprehensionClaims([finding]);

            Assert.True(finding.Q1);
            Assert.True(finding.Q2);
            Assert.False(finding.Q3);
            Assert.Equal(ErrorSeverity.major, GradeSubmissionCommandHandler.DeriveSeverity(finding));
        }

        [Fact]
        public void The_same_defect_found_twice_collapses_to_one_entry()
        {
            var merged = GradeSubmissionCommandHandler.MergeCollectedFindings(
            [
                Finding("E1", q1: true),
                Finding("S1", q1: true),
            ]);

            Assert.Single(merged);
            Assert.Equal("F1", merged[0].Id);
        }

        [Fact]
        public void A_duplicate_keeps_the_harsher_reading_rather_than_the_first_one_seen()
        {
            // The evidence pass called it a wording slip; the sweep pass, working from the
            // easy-to-miss checklist, saw that it reverses the mechanism. The harsher answer has
            // to survive, or the extra pass is worse than useless.
            var merged = GradeSubmissionCommandHandler.MergeCollectedFindings(
            [
                Finding("E1", q1: true),
                Finding("S1", q1: true, q3: true, explanation: "更长的说明：主客关系颠倒，读者需要回读。"),
            ]);

            var only = Assert.Single(merged);
            Assert.Equal(ErrorSeverity.major, GradeSubmissionCommandHandler.DeriveSeverity(only));
            Assert.Contains("主客关系颠倒", only.Explanation);
        }

        [Fact]
        public void The_same_span_on_two_dimensions_stays_two_findings()
        {
            // 晒斑 for "sunburn" is two independent defects: the referent is a different clinical
            // entity (meaning_transfer), and the text goes on to call the same thing 晒伤
            // elsewhere (textual_norms). Collapsing them would starve textual_norms, which is
            // exactly how it came out at Band 1 with "no evidence".
            var merged = GradeSubmissionCommandHandler.MergeCollectedFindings(
            [
                Finding("E1", dimension: "meaning_transfer", category: "distortion", q1: true, q3: true),
                Finding("P1", dimension: "textual_norms", category: "inappropriate_register", q1: true),
            ]);

            Assert.Equal(2, merged.Count);
            Assert.Equal(["F1", "F2"], merged.Select(f => f.Id));
        }

        [Fact]
        public void Different_spans_are_never_merged()
        {
            var merged = GradeSubmissionCommandHandler.MergeCollectedFindings(
            [
                Finding("E1", userSnippet: "晒斑"),
                Finding("E2", userSnippet: "表明着"),
            ]);

            Assert.Equal(2, merged.Count);
        }

        [Fact]
        public void Punctuation_and_spacing_do_not_stop_two_passes_matching_the_same_span()
        {
            var merged = GradeSubmissionCommandHandler.MergeCollectedFindings(
            [
                Finding("E1", userSnippet: "带有某些免疫疾病的人"),
                Finding("P1", userSnippet: "「带有某些免疫疾病的人」，"),
            ]);

            Assert.Single(merged);
        }

        [Fact]
        public void Findings_that_quote_nothing_are_each_kept_separately()
        {
            // Without the guard every snippet-less finding shares one key and all but the first
            // would silently vanish.
            var merged = GradeSubmissionCommandHandler.MergeCollectedFindings(
            [
                Finding("E1", userSnippet: ""),
                Finding("E2", userSnippet: ""),
            ]);

            Assert.Equal(2, merged.Count);
        }

        [Fact]
        public void A_source_snippet_missing_from_the_monolingual_pass_is_filled_in_from_the_other()
        {
            // The proofread stage never sees the source, so its findings carry no source snippet.
            var monolingual = Finding("P1");
            monolingual.SourceTextSnippet = null;

            var merged = GradeSubmissionCommandHandler.MergeCollectedFindings([monolingual, Finding("S1")]);

            Assert.Equal("Sunburn", Assert.Single(merged).SourceTextSnippet);
        }
    }
}
