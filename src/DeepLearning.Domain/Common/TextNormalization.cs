namespace DeepLearning.Domain.Common
{
    /// <summary>
    /// Two distinct normalization semantics that used to be hand-copied under different names
    /// (代码复用扫描_07_优化计划.md §3.1):
    ///
    /// <para><see cref="CanonicalKey"/> — case/whitespace-insensitive identity key (dedup, glossary
    /// lookup). Was <c>NormalizeCanonicalKey</c> in GenerateDeepLearningContentCommandHandler
    /// (capped at 255, matching the canonical_key column) and <c>Normalize</c> in
    /// VocabSemanticDriftService (same logic, but uncapped — a real inconsistency: two "the same
    /// key" computations that could disagree on a string longer than the column. Currently latent
    /// since VocabSemanticDriftService only ever normalizes VocabGlossaryEntry.EnglishExpr, which
    /// is itself DB-capped at 255 — but the two functions computing the same fact differently is
    /// the kind of drift that bites the next caller who isn't so lucky.)</para>
    ///
    /// <para><see cref="StripToAlphanumeric"/> — punctuation-/whitespace-insensitive substring
    /// matching (case preserved). Was <c>Normalise</c> in GradeSubmissionCommandHandler. A
    /// different semantic on purpose: fuzzy-matching a quoted snippet against source text should
    /// not case-fold "Word" into equivalence with "word".</para>
    /// </summary>
    public static class TextNormalization
    {
        private const int CanonicalKeyMaxLength = 255;

        /// <summary>Lowercase, trim, collapse internal whitespace to single spaces, cap at the
        /// canonical_key column length. Null/blank -&gt; null.</summary>
        public static string? CanonicalKey(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var collapsed = string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            var normalized = collapsed.ToLowerInvariant();
            return normalized.Length > CanonicalKeyMaxLength ? normalized[..CanonicalKeyMaxLength] : normalized;
        }

        /// <summary>Keeps only letters and digits — case preserved, no length cap. Null -&gt;
        /// empty string (not null), matching the punctuation-insensitive-matching call sites that
        /// always want a string to search/compare, never a null to check for.</summary>
        public static string StripToAlphanumeric(string? text)
            => new((text ?? string.Empty).Where(char.IsLetterOrDigit).ToArray());
    }
}
