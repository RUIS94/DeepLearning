namespace DeepLearning.Application.Interfaces
{
    /// <summary>One recurring expression to check: this passage's sense vs. what the glossary already records.</summary>
    public record VocabDriftItem(
        string CanonicalKey,
        string EnglishExpr,
        string? ThisChinese,
        string? ThisNote,
        string KnownSemantics);

    /// <summary>
    /// Runs <c>AiOperationType.vocab_semantic_drift</c> — one batched call over every recurring
    /// expression for a single question, deciding per expression whether this passage introduces
    /// a sense/usage/register not already covered by the glossary's accumulated semantics.
    ///
    /// <para>Contract: NEVER throws. The returned map holds only the entries that changed
    /// (<c>canonicalKey -&gt; the full new accumulated-semantics text</c>); an omitted key means
    /// "no new sense — leave the glossary row untouched".</para>
    /// </summary>
    public interface IVocabSemanticDriftService
    {
        Task<IReadOnlyDictionary<string, string>> AnalyzeAsync(
            Guid examTypeId,
            IReadOnlyList<VocabDriftItem> items,
            string sourceText,
            string taskType,
            CancellationToken cancellationToken = default);
    }
}
