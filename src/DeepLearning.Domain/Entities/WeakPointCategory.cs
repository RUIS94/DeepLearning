using DeepLearning.Domain.Common;

namespace DeepLearning.Domain.Entities
{
    /// <summary>
    /// One of the 8 fixed, global top-level linguistic error categories (Semantic / Lexical /
    /// Grammatical-Syntactic / Information / Discourse-Logic / Pragmatic-Stylistic /
    /// Fluency-Naturalness / Mechanics — see 薄弱点分类与生命周期管理_策划书.md §1). This set is a
    /// closed list maintained by seed data only; nothing mints a new row at runtime the way
    /// <see cref="WeakPointCatalog"/> leaves can.
    /// </summary>
    public class WeakPointCategory : AggregateRoot
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int DisplayOrder { get; set; }
    }
}
