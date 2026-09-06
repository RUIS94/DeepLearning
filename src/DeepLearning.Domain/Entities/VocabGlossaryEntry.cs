using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    /// <summary>
    /// One row per distinct expression (keyed by <see cref="CanonicalKey"/>) — the living,
    /// cross-question record of what that expression means, maintained by AI.
    ///
    /// <para>Distinct from <see cref="VocabExpression"/>, which is a per-question snapshot frozen
    /// at deep-learning generation time: a word appearing in five passages produces five
    /// <see cref="VocabExpression"/> rows (one per passage, each with that passage's sense) and a
    /// single <see cref="VocabGlossaryEntry"/>. The glossary entry is seeded deterministically
    /// from the first occurrence; on every later occurrence a
    /// <c>vocab_semantic_drift</c> AI call decides whether that passage introduces a sense not
    /// already covered by <see cref="AccumulatedSemantics"/> and, if so, folds it in.</para>
    ///
    /// <para>This is the review library's vocab data source — one entry per word, all senses —
    /// and the target of <c>user_vocab_review.vocab_id</c>.</para>
    /// </summary>
    public class VocabGlossaryEntry : Entity
    {
        /// <summary>
        /// Normalised English expression (lower-cased, trimmed, whitespace collapsed), mirroring
        /// <see cref="VocabExpression.CanonicalKey"/>. Unique across the table.
        /// </summary>
        public string CanonicalKey { get; set; } = string.Empty;

        /// <summary>A representative surface form, taken from the first occurrence.</summary>
        public string EnglishExpr { get; set; } = string.Empty;

        /// <summary>
        /// The AI-maintained account of every sense / usage / register this expression has been
        /// seen in. Seeded from the first occurrence's context note; grown (never rewritten) when
        /// a later occurrence introduces a new sense.
        /// </summary>
        public string AccumulatedSemantics { get; set; } = string.Empty;

        /// <summary>Representative Chinese rendering, from the first occurrence.</summary>
        public string? ChineseEquiv { get; set; }

        public string? Category { get; set; }
        public string? Domain { get; set; }
        public string? Scenario { get; set; }
        public string? FrequencyTag { get; set; }

        /// <summary>Distinct senses recorded in <see cref="AccumulatedSemantics"/>. Starts at 1.</summary>
        public int SenseCount { get; set; } = 1;

        /// <summary>Total times this expression has surfaced across all questions. Starts at 1.</summary>
        public int OccurrenceCount { get; set; } = 1;

        public Guid? FirstSeenQuestionId { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
