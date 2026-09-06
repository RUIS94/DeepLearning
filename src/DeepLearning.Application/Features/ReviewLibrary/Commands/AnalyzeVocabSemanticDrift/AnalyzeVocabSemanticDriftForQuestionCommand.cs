using MediatR;

namespace DeepLearning.Application.Features.ReviewLibrary.Commands.AnalyzeVocabSemanticDrift
{
    /// <summary>
    /// For every expression this question surfaced that also appears on another question, check
    /// whether this passage's sense is already covered by its <c>vocab_glossary</c> row and, if
    /// not, fold the new sense in. Enqueued by <see cref="Interfaces.IVocabGlossaryQueue"/> after
    /// a first-time deep-learning generation; a no-op when nothing recurs.
    ///
    /// <para>Only ever writes <c>vocab_glossary</c> — the frozen per-question
    /// <c>vocab_expressions</c> rows are never touched.</para>
    /// </summary>
    public record AnalyzeVocabSemanticDriftForQuestionCommand(Guid QuestionId, Guid ExamTypeId) : IRequest;
}
