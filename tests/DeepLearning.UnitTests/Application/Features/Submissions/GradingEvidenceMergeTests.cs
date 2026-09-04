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
        /// <summary>
        /// The submission the fixtures' snippets are quoted from. Merging resolves each snippet to
        /// a character range in this text, so the snippets have to actually occur in it.
        /// </summary>
        private const string Translation =
            "晒斑表明着人们曾处在阳光之下。被破坏的RNA会释放一种被晒伤的信号。带有某些免疫疾病的人会体会到灼烧感。";

        private static GradeSubmissionCommandHandler.Finding Finding(
            string id,
            string userSnippet = "晒斑表明着",
            string dimension = "meaning_transfer",
            string category = "distortion",
            bool q1 = false,
            bool q2 = false,
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
                Summary = "摘要",
                Explanation = explanation,
                Suggestion = "建议",
            };

        [Theory]
        // NAATI's own definition, and the whole of it: Major iff it affects intent or
        // purpose/function (q1), and/or impacts comprehension (q2). Nothing else moves the level.
        [InlineData(false, false, ErrorSeverity.minor)]
        [InlineData(false, true, ErrorSeverity.major)]
        [InlineData(true, false, ErrorSeverity.major)]
        [InlineData(true, true, ErrorSeverity.major)]
        public void Severity_is_the_official_two_level_test(bool q1, bool q2, ErrorSeverity expected)
        {
            var severity = GradeSubmissionCommandHandler.DeriveSeverity(Finding("E1", q1: q1, q2: q2));

            Assert.Equal(expected, severity);
        }

        [Fact]
        public void A_comprehension_claim_with_no_wrong_reading_named_is_demoted()
        {
            // q2 alone promotes an error to officially Major, and the models answer it far too
            // readily: the first v3 run scored "還尚 is redundant, it affects fluency" as major.
            // The prompt requires the claim to be paid for by naming what the reader ends up
            // believing; this applies the prompt's own fallback when it is not.
            var unpaid = Finding("E1", q2: true);
            var paid = Finding("E2", userSnippet: "被晒伤的信号", q2: true);
            paid.Q2WrongReading = "读者会以为信号本身被晒伤了";

            var findings = new List<GradeSubmissionCommandHandler.Finding> { unpaid, paid };
            GradeSubmissionCommandHandler.NormaliseComprehensionClaims(findings);

            Assert.False(unpaid.Q2);
            Assert.Equal(ErrorSeverity.minor, GradeSubmissionCommandHandler.DeriveSeverity(unpaid));
            Assert.True(paid.Q2);
            Assert.Equal(ErrorSeverity.major, GradeSubmissionCommandHandler.DeriveSeverity(paid));
        }

        [Fact]
        public void Demotion_leaves_the_intent_question_untouched()
        {
            // Only the unsubstantiated q2 is withdrawn — an error that genuinely changed the
            // intent stays Major on q1 alone, with or without a named wrong reading.
            var finding = Finding("E1", q1: true, q2: true);

            GradeSubmissionCommandHandler.NormaliseComprehensionClaims([finding]);

            Assert.True(finding.Q1);
            Assert.False(finding.Q2);
            Assert.Equal(ErrorSeverity.major, GradeSubmissionCommandHandler.DeriveSeverity(finding));
        }

        [Fact]
        public void The_same_defect_found_twice_collapses_to_one_entry()
        {
            var merged = GradeSubmissionCommandHandler.MergeCollectedFindings(
            [
                Finding("E1"),
                Finding("S1"),
            ],
                Translation);

            Assert.Single(merged);
            Assert.Equal("F1", merged[0].Id);
        }

        [Fact]
        public void A_duplicate_keeps_the_harsher_reading_rather_than_the_first_one_seen()
        {
            // The evidence pass called it a wording slip; the sweep pass, working from the
            // easy-to-miss checklist, saw that it reverses the mechanism. The harsher answer has
            // to survive, or the extra pass is worse than useless.
            var strict = Finding("S1", q2: true, explanation: "更长的说明：主客关系颠倒，读者会读错。");
            strict.Q2WrongReading = "读者会以为信号本身被晒伤了";

            var merged = GradeSubmissionCommandHandler.MergeCollectedFindings(
            [
                Finding("E1"),
                strict,
            ],
                Translation);

            var only = Assert.Single(merged);
            Assert.Equal(ErrorSeverity.major, GradeSubmissionCommandHandler.DeriveSeverity(only));
            Assert.Contains("主客关系颠倒", only.Explanation);
            Assert.Equal("读者会以为信号本身被晒伤了", only.Q2WrongReading);
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
                Finding("E1", dimension: "meaning_transfer", category: "distortion", q2: true),
                Finding("P1", dimension: "textual_norms", category: "inappropriate_register"),
            ],
                Translation);

            Assert.Equal(2, merged.Count);
            Assert.Equal(["F1", "F2"], merged.Select(f => f.Id));
        }

        [Fact]
        public void Different_spans_are_never_merged()
        {
            var merged = GradeSubmissionCommandHandler.MergeCollectedFindings(
            [
                Finding("E1", userSnippet: "晒斑表明着"),
                Finding("E2", userSnippet: "带有某些免疫疾病的人"),
            ],
                Translation);

            Assert.Equal(2, merged.Count);
        }

        [Fact]
        public void Punctuation_and_spacing_do_not_stop_two_passes_matching_the_same_span()
        {
            var merged = GradeSubmissionCommandHandler.MergeCollectedFindings(
            [
                Finding("E1", userSnippet: "带有某些免疫疾病的人"),
                Finding("P1", userSnippet: "「带有某些免疫疾病的人」，"),
            ],
                Translation);

            Assert.Single(merged);
        }

        [Fact]
        public void Two_stages_quoting_the_same_place_at_different_lengths_still_merge()
        {
            // The real shape of a duplicate: one stage takes the clause, another the whole
            // sentence. Matching on snippet equality misses this and leaves both in — which now
            // matters twice over, because the verdict stage is handed counted coverage figures
            // and a duplicate inflates them.
            var clause = Finding("E1", userSnippet: "释放一种被晒伤的信号");
            var sentence = Finding("S1", userSnippet: "被破坏的RNA会释放一种被晒伤的信号");

            var merged = GradeSubmissionCommandHandler.MergeCollectedFindings([clause, sentence], Translation);

            Assert.Single(merged);
        }

        [Fact]
        public void Neighbouring_but_non_overlapping_spans_are_left_alone()
        {
            var first = Finding("E1", userSnippet: "晒斑表明着");
            var second = Finding("E2", userSnippet: "带有某些免疫疾病的人");

            var merged = GradeSubmissionCommandHandler.MergeCollectedFindings([first, second], Translation);

            Assert.Equal(2, merged.Count);
        }

        [Fact]
        public void The_wrong_reading_travels_with_the_comprehension_claim()
        {
            // The lenient stage is seen first, so without carrying this the merged finding would
            // claim q2 with nothing behind it — and NormaliseComprehensionClaims would then
            // rightly demote it, silently discarding the stricter stage's judgement.
            var lenient = Finding("E1", userSnippet: "释放一种被晒伤的信号");
            var strict = Finding("S1", userSnippet: "释放一种被晒伤的信号", q2: true);
            strict.Q2WrongReading = "读者会以为信号本身被晒伤了";

            var merged = GradeSubmissionCommandHandler.MergeCollectedFindings([lenient, strict], Translation);

            var only = Assert.Single(merged);
            Assert.True(only.Q2);
            Assert.Equal("读者会以为信号本身被晒伤了", only.Q2WrongReading);
            Assert.Equal(ErrorSeverity.major, GradeSubmissionCommandHandler.DeriveSeverity(only));
        }

        [Fact]
        public void A_merged_comprehension_claim_nobody_substantiated_is_still_demoted()
        {
            var first = Finding("E1", userSnippet: "释放一种被晒伤的信号", q2: true);
            var second = Finding("S1", userSnippet: "释放一种被晒伤的信号", q2: true);

            var merged = GradeSubmissionCommandHandler.MergeCollectedFindings([first, second], Translation);

            var only = Assert.Single(merged);
            Assert.False(only.Q2);
            Assert.Equal(ErrorSeverity.minor, GradeSubmissionCommandHandler.DeriveSeverity(only));
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
            ],
                Translation);

            Assert.Equal(2, merged.Count);
        }

        [Fact]
        public void A_source_snippet_missing_from_the_monolingual_pass_is_filled_in_from_the_other()
        {
            // The proofread stage never sees the source, so its findings carry no source snippet.
            var monolingual = Finding("P1");
            monolingual.SourceTextSnippet = null;

            var merged = GradeSubmissionCommandHandler.MergeCollectedFindings([monolingual, Finding("S1")], Translation);

            Assert.Equal("Sunburn", Assert.Single(merged).SourceTextSnippet);
        }

        [Fact]
        public void The_emitted_answers_land_on_the_intent_and_comprehension_questions()
        {
            var finding = new GradeSubmissionCommandHandler.Finding
            {
                Id = "E1",
                RawQ1 = true,
                RawQ2 = false,
                RawQ2WrongReading = null,
            };

            GradeSubmissionCommandHandler.NormaliseQuestionScheme([finding]);

            Assert.True(finding.Q1);
            Assert.False(finding.Q2);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData(true, null)]
        [InlineData(null, true)]
        public void A_finding_that_left_either_question_unanswered_is_rejected_rather_than_defaulted(
            bool? q1, bool? q2)
        {
            // Defaulting a missing answer to false makes the finding a Minor, so the mistake
            // does not look like one: a whole run of them reads as a clean grading of a
            // competent translation. That has to be loud.
            var finding = new GradeSubmissionCommandHandler.Finding { Id = "E1", RawQ1 = q1, RawQ2 = q2 };

            Assert.Throws<InvalidOperationException>(
                () => GradeSubmissionCommandHandler.NormaliseQuestionScheme([finding]));
        }

        [Fact]
        public void A_truncated_attempt_is_re_prompted_to_finish_not_to_fix_a_field()
        {
            // The generic notice is wrong twice over for a cut-off response: it points the model
            // at its errorCategory values, which were fine, and it says "fix just this one thing
            // and keep everything else" — which, for a length problem, can only be obeyed by
            // dropping findings. That is the one thing a collection stage must never do.
            var notice = GradeSubmissionCommandHandler.BuildRejectionNotice(
                "output was cut off at the 16384-token cap (provider reported truncation), not malformed.",
                truncated: true);

            Assert.Contains("没有写完", notice);
            Assert.Contains("不要为了变短而减少 findings", notice);
            Assert.DoesNotContain("errorCategory 只能取错误类别", notice);
            Assert.DoesNotContain("请只修正这一处", notice);
        }

        [Fact]
        public void A_malformed_attempt_still_gets_the_field_level_advice()
        {
            var notice = GradeSubmissionCommandHandler.BuildRejectionNotice(
                "error_category 'textual_norms' is not a known error taxonomy for this exam type.",
                truncated: false);

            Assert.Contains("errorCategory 只能取错误类别", notice);
            Assert.Contains("请只修正这一处", notice);
            Assert.DoesNotContain("没有写完", notice);
        }

        [Fact]
        public void A_first_attempt_carries_no_notice_at_all()
        {
            Assert.Equal(string.Empty, GradeSubmissionCommandHandler.BuildRejectionNotice(null, truncated: false));
        }
    }
}
