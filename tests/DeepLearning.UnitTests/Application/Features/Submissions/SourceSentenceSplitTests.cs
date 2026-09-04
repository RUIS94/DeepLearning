using DeepLearning.Application.Features.Submissions.Commands.GradeSubmission;

namespace DeepLearning.UnitTests.Application.Features.Submissions
{
    /// <summary>
    /// The evidence stage is handed a fixed, numbered list of source sentences and only fills in a
    /// status for each. That is the whole point: when the model split the text itself, the
    /// coverage check was decorative — a model that stopped early could emit fewer rows and still
    /// look complete, and nothing could tell a short text from a lazy read.
    ///
    /// <para>These tests do not ask for linguistically perfect segmentation. They ask for the
    /// property the pipeline actually depends on: the same text always yields the same slots.</para>
    /// </summary>
    public class SourceSentenceSplitTests
    {
        [Fact]
        public void A_title_run_into_the_body_does_not_swallow_the_first_sentence()
        {
            // The real text this pipeline was built against opens exactly like this — a headline
            // and the first sentence with no full stop between them. Told to "split on full
            // stops", a model starts out wrong here and every slot after it is off.
            var sentences = GradeSubmissionCommandHandler.SplitSentences(
                "Why people get sunburn Sunburn shows that people have been in the sun. Now, researchers have discovered a signal.");

            Assert.Equal(2, sentences.Count);
            Assert.StartsWith("Why people get sunburn", sentences[0]);
            Assert.EndsWith("in the sun.", sentences[0]);
            Assert.Equal("Now, researchers have discovered a signal.", sentences[1]);
        }

        [Fact]
        public void Paragraphs_are_split_even_without_terminating_punctuation()
        {
            var sentences = GradeSubmissionCommandHandler.SplitSentences("First paragraph\n\nSecond paragraph");

            Assert.Equal(["First paragraph", "Second paragraph"], sentences);
        }

        [Fact]
        public void Question_and_exclamation_marks_end_a_sentence()
        {
            var sentences = GradeSubmissionCommandHandler.SplitSentences("Why does it happen? Nobody knew! Now we do.");

            Assert.Equal(3, sentences.Count);
        }

        [Fact]
        public void A_decimal_point_does_not_end_a_sentence()
        {
            var sentences = GradeSubmissionCommandHandler.SplitSentences("Rates rose by 3.5 per cent. That is a lot.");

            Assert.Equal(2, sentences.Count);
            Assert.Contains("3.5", sentences[0]);
        }

        [Fact]
        public void Common_abbreviations_do_not_end_a_sentence()
        {
            var sentences = GradeSubmissionCommandHandler.SplitSentences(
                "Some conditions, e.g. psoriasis, are treated with light. Others are not.");

            Assert.Equal(2, sentences.Count);
            Assert.Contains("e.g. psoriasis", sentences[0]);
        }

        [Fact]
        public void The_split_is_stable_across_calls()
        {
            // Stability is the property that matters — the model is only labelling slots, so a
            // split that varied between attempts would make the retry feedback nonsense.
            const string text = "One. Two? Three! Four\n\nFive.";

            Assert.Equal(
                GradeSubmissionCommandHandler.SplitSentences(text),
                GradeSubmissionCommandHandler.SplitSentences(text));
        }

        [Fact]
        public void A_text_with_no_terminator_at_all_is_still_one_slot()
        {
            var sentences = GradeSubmissionCommandHandler.SplitSentences("a single unterminated line");

            Assert.Single(sentences);
        }

        [Fact]
        public void An_empty_source_never_yields_zero_slots()
        {
            // Zero slots would make the coverage check vacuously true.
            Assert.Single(GradeSubmissionCommandHandler.SplitSentences("   "));
        }
    }
}
