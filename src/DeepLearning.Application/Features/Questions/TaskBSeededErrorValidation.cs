namespace DeepLearning.Application.Features.Questions
{
    /// <summary>
    /// Task B seeded-error range validation: start &lt; end, in-bounds against the flawed text
    /// length, and no two ranges overlap after sorting by start. Shared by
    /// ImportUserQuestionValidator (manually-imported questions) and
    /// GenerateQuestionCommandHandler.ValidateAndBuildTaskBSeededErrors (AI-generated questions) —
    /// 代码复用扫描_07_优化计划.md §3.4/N10.
    ///
    /// The two callers deliberately diverge on one point: only the AI-generation path also
    /// validates that each error's category key is a known taxonomy for the exam type —
    /// <paramref name="knownCategoryKeys"/>/the category checks below are optional for exactly
    /// that reason (ImportUserQuestionValidator doesn't have/want that check on the manual-import
    /// path; see GenerateQuestionControllerTests.Generate_rejects_a_task_b_response_whose_seeded_error_category_is_not_a_known_taxonomy,
    /// which pins this asymmetry).
    /// </summary>
    public static class TaskBSeededErrorValidation
    {
        public readonly record struct Range(int Start, int End, string Category);

        public static bool AllWithinBounds(IEnumerable<Range> errors, int textLength)
            => errors.All(e => e.Start >= 0 && e.End > e.Start && e.End <= textLength);

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
        /// Throws on the first violation found (bounds, then category if
        /// <paramref name="knownCategoryKeys"/> is supplied, then overlap) — the exception-throwing
        /// counterpart to the boolean predicates above, for a caller (GenerateQuestionCommandHandler)
        /// that wants to reject a malformed AI response outright rather than report which
        /// FluentValidation rule failed.
        /// </summary>
        public static void Validate(IReadOnlyList<Range> errors, int textLength, ISet<string>? knownCategoryKeys, string textLabel)
        {
            if (!AllWithinBounds(errors, textLength))
            {
                var bad = errors.First(e => e.Start < 0 || e.End <= e.Start || e.End > textLength);
                throw new InvalidOperationException(
                    $"seededError position [{bad.Start},{bad.End}) is out of bounds for the {textLength}-character {textLabel}.");
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
