using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    public class VocabExpression : Entity
    {
        public Guid? QuestionId { get; set; }
        public string EnglishExpr { get; set; } = string.Empty;
        public string? ChineseEquiv { get; set; }
        public string? ContextNote { get; set; }
        public string? Category { get; set; }
        public string? Domain { get; set; }
        public string? Scenario { get; set; }
        public string? FrequencyTag { get; set; }

        /// <summary>
        /// Whether the expression can be rendered literally: false for idioms / figurative
        /// phrases whose meaning diverges from the words (design doc §9 "不可机械直译的须明确
        /// 标记"), true for safely literal ones, null when unclear.
        /// </summary>
        public bool? LiteralTranslatable { get; set; }

        /// <summary>
        /// Normalised <see cref="EnglishExpr"/> (lower-cased, trimmed, whitespace collapsed) used
        /// to spot the same expression recurring in a later question's source text so the deep
        /// learning prompt can ask for it to be re-explained in the new context rather than
        /// duplicated verbatim (design doc §9).
        /// </summary>
        public string? CanonicalKey { get; set; }
        public DateTimeOffset CreatedAt { get; set; }

        public Question? Question { get; set; }
    }
}
