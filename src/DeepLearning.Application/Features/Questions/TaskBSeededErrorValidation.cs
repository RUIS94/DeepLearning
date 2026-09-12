namespace DeepLearning.Application.Features.Questions
{
    /// <summary>
    /// Task B seeded-error range validation: start &lt; end, in-bounds against the flawed text
    /// length, and no two ranges overlap after sorting by start. Shared by
    /// ImportUserQuestionValidator (manually-imported questions) and
    /// GenerateQuestionCommandHandler.ValidateAndBuildTaskBSeededErrors (AI-generated questions) —
    /// 代码复用扫描_07_优化计划.md §3.4/N10.
    ///
    /// The two callers deliberately diverge on two points, both AI-generation-only because a
    /// human authoring a question by hand doesn't need the AI checked for lying to itself:
    /// (1) each error's category key must be a known taxonomy for the exam type
    /// (<c>knownCategoryKeys</c>; see
    /// GenerateQuestionControllerTests.Generate_rejects_a_task_b_response_whose_seeded_error_category_is_not_a_known_taxonomy);
    /// (2) each error's CorrectReferenceText must not already appear verbatim in the flawed text
    /// (<c>flawedText</c> — see <see cref="AllActuallyFlawed"/>'s doc comment for the 2026-09-12
    /// real example this was found from).
    /// </summary>
    public static class TaskBSeededErrorValidation
    {
        public readonly record struct Range(int Start, int End, string Category, string CorrectReferenceText = "");

        public static bool AllWithinBounds(IEnumerable<Range> errors, int textLength)
            => errors.All(e => e.Start >= 0 && e.End > e.Start && e.End <= textLength);

        /// <summary>
        /// Catches a distinct failure mode from a bad position: the AI describes a mistranslation
        /// and gives <see cref="Range.CorrectReferenceText"/> as the fix, but never actually wrote
        /// the flaw into the text — <paramref name="flawedText"/> already contains
        /// CorrectReferenceText verbatim (usually at the AI's own miscounted position, so a bounds
        /// check alone won't catch it — see the 2026-09-12 NAATI CT Task B sample where 4 of 5
        /// seededErrors had this shape). When that happens the seeded "error" is unsolvable: the
        /// user is shown text that already reads as its own correction, with nothing to find.
        ///
        /// <para>Skips anything shorter than <see cref="MinCheckedReferenceLength"/> characters —
        /// a one- or two-character CorrectReferenceText (a lone punctuation mark, a single common
        /// word for a spelling/punctuation-category error) is likely to recur elsewhere in the text
        /// for entirely unrelated, legitimate reasons, and a false rejection here burns a retry for
        /// nothing.</para>
        ///
        /// Only meaningful for the AI-generation path (<see cref="Validate"/>'s optional
        /// <c>flawedText</c> parameter) — a human manually authoring a question via
        /// ImportUserQuestionValidator does not go through this method at all.
        /// </summary>
        public static bool AllActuallyFlawed(IEnumerable<Range> errors, string flawedText)
            => errors.All(e => e.CorrectReferenceText.Length < MinCheckedReferenceLength
                || !flawedText.Contains(e.CorrectReferenceText, StringComparison.Ordinal));

        private const int MinCheckedReferenceLength = 3;

        public static bool NoOverlaps(IEnumerable<Range> errors)
        {
            var sorted = errors.OrderBy(e => e.Start).ToList();
            for (var i = 1; i < sorted.Count; i++)
            {
                if (sorted[i].Start < sorted[i - 1].End)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool AllKnownCategories(IEnumerable<Range> errors, ISet<string> knownCategoryKeys)
            => errors.All(e => knownCategoryKeys.Contains(e.Category));

        /// <summary>
        /// Throws on the first violation found (bounds, then "was the flaw actually written" if
        /// <paramref name="flawedText"/> is supplied, then category if
        /// <paramref name="knownCategoryKeys"/> is supplied, then overlap) — the exception-throwing
        /// counterpart to the boolean predicates above, for a caller (GenerateQuestionCommandHandler)
        /// that wants to reject a malformed AI response outright rather than report which
        /// FluentValidation rule failed.
        /// </summary>
        public static void Validate(IReadOnlyList<Range> errors, int textLength, ISet<string>? knownCategoryKeys, string textLabel, string? flawedText = null)
        {
            if (!AllWithinBounds(errors, textLength))
            {
                var bad = errors.First(e => e.Start < 0 || e.End <= e.Start || e.End > textLength);
                throw new InvalidOperationException(
                    $"seededError position [{bad.Start},{bad.End}) is out of bounds for the {textLength}-character {textLabel}.");
            }

            if (flawedText is not null && !AllActuallyFlawed(errors, flawedText))
            {
                var bad = errors.First(e => e.CorrectReferenceText.Length >= MinCheckedReferenceLength
                    && flawedText.Contains(e.CorrectReferenceText, StringComparison.Ordinal));
                throw new InvalidOperationException(
                    $"seededError correctReferenceText '{bad.CorrectReferenceText}' already appears verbatim in the {textLabel} — " +
                    "the described mistake was never actually written into the text (it already reads as its own correction), " +
                    "so there is nothing there for the user to find and fix. Actually corrupt the text at the described error's " +
                    "location (or replace this seededError with a real one that matches what the text says).");
            }

            if (knownCategoryKeys is not null && !AllKnownCategories(errors, knownCategoryKeys))
            {
                var bad = errors.First(e => !knownCategoryKeys.Contains(e.Category));
                throw new InvalidOperationException($"seededError errorCategory '{bad.Category}' is not a known error taxonomy for this exam type.");
            }

            if (!NoOverlaps(errors))
            {
                throw new InvalidOperationException("seededError position ranges must not overlap.");
            }
        }
    }
}
