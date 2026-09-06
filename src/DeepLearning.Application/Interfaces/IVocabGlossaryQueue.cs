namespace DeepLearning.Application.Interfaces
{
    /// <summary>
    /// Hands vocab-glossary semantic-drift analysis to a background worker after a deep-learning
    /// generation that surfaced at least one recurring expression. Fire-and-forget: the
    /// generation response does not wait on it, and a failed run just leaves the glossary at its
    /// current state.
    /// </summary>
    public interface IVocabGlossaryQueue
    {
        Task EnqueueAsync(Guid questionId, Guid examTypeId, CancellationToken cancellationToken = default);
    }
}
