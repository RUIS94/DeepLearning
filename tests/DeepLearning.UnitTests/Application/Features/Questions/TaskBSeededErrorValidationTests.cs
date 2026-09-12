using static DeepLearning.Application.Features.Questions.TaskBSeededErrorValidation;
using Range = DeepLearning.Application.Features.Questions.TaskBSeededErrorValidation.Range;

namespace DeepLearning.UnitTests.Application.Features.Questions
{
    public class TaskBSeededErrorValidationTests
    {
        [Theory]
        [InlineData(0, 5, 10, true)]
        [InlineData(0, 10, 10, true)] // touches the end exactly — allowed
        [InlineData(-1, 5, 10, false)] // negative start
        [InlineData(5, 5, 10, false)] // end == start (empty range)
        [InlineData(5, 3, 10, false)] // end < start (reversed)
        [InlineData(0, 11, 10, false)] // end past textLength
        public void AllWithinBounds_checks_start_end_and_textLength(int start, int end, int textLength, bool expected)
        {
            var errors = new[] { new Range(start, end, "cat") };

            Assert.Equal(expected, AllWithinBounds(errors, textLength));
        }

        [Fact]
        public void AllWithinBounds_is_true_for_an_empty_list()
        {
            Assert.True(AllWithinBounds([], 10));
        }

        [Fact]
        public void NoOverlaps_is_true_when_ranges_are_disjoint()
        {
            var errors = new[]
            {
                new Range(0, 5, "a"),
                new Range(5, 10, "b"), // touches but does not overlap
                new Range(10, 15, "c"),
            };

            Assert.True(NoOverlaps(errors));
        }

        [Fact]
        public void NoOverlaps_is_false_when_two_ranges_overlap()
        {
            var errors = new[]
            {
                new Range(0, 5, "a"),
                new Range(4, 10, "b"), // starts before the previous one ends
            };

            Assert.False(NoOverlaps(errors));
        }

        [Fact]
        public void NoOverlaps_sorts_before_comparing_so_input_order_does_not_matter()
        {
            var errors = new[]
            {
                new Range(10, 15, "c"),
                new Range(0, 5, "a"),
                new Range(4, 9, "b"), // overlaps [0,5) even though it's listed after [10,15)
            };

            Assert.False(NoOverlaps(errors));
        }

        [Fact]
        public void NoOverlaps_is_true_for_zero_or_one_ranges()
        {
            Assert.True(NoOverlaps([]));
            Assert.True(NoOverlaps([new Range(0, 5, "a")]));
        }

        [Fact]
        public void AllKnownCategories_is_true_only_when_every_category_is_in_the_known_set()
        {
            var known = new HashSet<string> { "grammar", "meaning" };

            Assert.True(AllKnownCategories([new Range(0, 5, "grammar")], known));
            Assert.False(AllKnownCategories([new Range(0, 5, "not_a_real_category")], known));
        }

        [Fact]
        public void AllActuallyFlawed_is_false_when_correctReferenceText_already_appears_in_the_flawed_text()
        {
            // The 2026-09-12 real case this check exists for: the AI's positions pointed at
            // unrelated text, but the "correction" it claimed to be fixing was sitting verbatim
            // elsewhere in the very text it was supposedly missing from — i.e. the mistake was
            // never actually written in, so there was nothing for a user to find.
            var flawedText = "免费公共项目已经开始实施。";
            var errors = new[] { new Range(0, 2, "unjustified_omission", "免费公共项目") };

            Assert.False(AllActuallyFlawed(errors, flawedText));
        }

        [Fact]
        public void AllActuallyFlawed_is_true_when_the_correction_does_not_appear_anywhere_in_the_text()
        {
            var flawedText = "该计划将使参观人数增长10%。";
            var errors = new[] { new Range(0, 2, "distortion", "15%") };

            Assert.True(AllActuallyFlawed(errors, flawedText));
        }

        [Fact]
        public void AllActuallyFlawed_ignores_errors_with_no_correctReferenceText()
        {
            Assert.True(AllActuallyFlawed([new Range(0, 2, "grammar", "")], "anything"));
        }

        [Fact]
        public void AllActuallyFlawed_ignores_a_one_or_two_character_correctReferenceText_even_if_it_recurs()
        {
            // A lone punctuation mark or common short word will legitimately recur elsewhere in
            // the text for unrelated reasons — checking it would just burn a retry on a false
            // positive, so anything under MinCheckedReferenceLength is exempt.
            var flawedText = "第一句，第二句，第三句。";

            Assert.True(AllActuallyFlawed([new Range(0, 2, "punctuation_error", "，")], flawedText));
        }

        [Fact]
        public void Validate_throws_on_out_of_bounds_before_checking_category_or_overlap()
        {
            // Out of bounds AND an unknown category in the same input — bounds must win (per the
            // class doc comment's stated check order: bounds, then category, then overlap).
            var errors = new List<Range> { new(0, 999, "not_a_real_category") };
            var known = new HashSet<string> { "grammar" };

            var ex = Assert.Throws<InvalidOperationException>(
                () => Validate(errors, textLength: 10, known, textLabel: "flawed translation"));
            Assert.Contains("out of bounds", ex.Message);
        }

        [Fact]
        public void Validate_throws_when_correctReferenceText_already_appears_in_flawedText()
        {
            var errors = new List<Range> { new(0, 2, "grammar", "免费公共项目") };

            var ex = Assert.Throws<InvalidOperationException>(
                () => Validate(errors, textLength: 20, knownCategoryKeys: null, textLabel: "flawed translation",
                    flawedText: "已经开始的免费公共项目实施方案。"));
            Assert.Contains("already appears verbatim", ex.Message);
        }

        [Fact]
        public void Validate_skips_the_already_flawed_check_when_flawedText_is_not_supplied()
        {
            // ImportUserQuestionValidator's path doesn't call Validate at all, but this pins that
            // any future caller omitting flawedText isn't forced into the AI-only check.
            var errors = new List<Range> { new(0, 2, "grammar", "免费公共项目") };

            Validate(errors, textLength: 20, knownCategoryKeys: null, textLabel: "flawed translation");
        }

        [Fact]
        public void Validate_throws_on_unknown_category_before_checking_overlap()
        {
            // Unknown category AND an overlap in the same input — category must win over overlap.
            var errors = new List<Range> { new(0, 5, "bad_cat"), new(3, 8, "bad_cat") };
            var known = new HashSet<string> { "grammar" };

            var ex = Assert.Throws<InvalidOperationException>(
                () => Validate(errors, textLength: 100, known, textLabel: "flawed translation"));
            Assert.Contains("not a known error taxonomy", ex.Message);
        }

        [Fact]
        public void Validate_skips_the_category_check_when_knownCategoryKeys_is_null()
        {
            // ImportUserQuestionValidator's path — category isn't checked there (see class doc
            // comment on the deliberate asymmetry with GenerateQuestionCommandHandler).
            var errors = new List<Range> { new(0, 5, "anything_goes"), new(6, 10, "anything_goes") };

            Validate(errors, textLength: 100, knownCategoryKeys: null, textLabel: "flawed translation");
            // No exception — the overlap check still runs and these two ranges don't overlap.
        }

        [Fact]
        public void Validate_throws_on_overlap_when_bounds_and_categories_are_fine()
        {
            var errors = new List<Range> { new(0, 5, "grammar"), new(3, 8, "grammar") };
            var known = new HashSet<string> { "grammar" };

            var ex = Assert.Throws<InvalidOperationException>(
                () => Validate(errors, textLength: 100, known, textLabel: "flawed translation"));
            Assert.Contains("must not overlap", ex.Message);
        }

        [Fact]
        public void Validate_does_not_throw_for_valid_non_overlapping_in_bounds_known_categories()
        {
            var errors = new List<Range> { new(0, 5, "grammar"), new(6, 10, "meaning") };
            var known = new HashSet<string> { "grammar", "meaning" };

            Validate(errors, textLength: 20, known, textLabel: "flawed translation");
        }

        [Fact]
        public void Validate_does_not_throw_for_an_empty_error_list()
        {
            Validate([], textLength: 20, knownCategoryKeys: null, textLabel: "flawed translation");
        }
    }
}
