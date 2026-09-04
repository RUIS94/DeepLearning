namespace DeepLearning.Application.Interfaces
{
    /// <summary>
    /// Hands weak-point extraction to a background worker once grading has finished.
    ///
    /// <para>Separate from <see cref="IGradingJobQueue"/> on purpose: they are queued at
    /// different moments by different callers, and the submission carries a distinct status for
    /// each, so collapsing them into one interface would only make each caller pass an argument
    /// saying which of the two it meant.</para>
    /// </summary>
    public interface IWeakPointGenerationQueue
    {
        Task EnqueueAsync(Guid submissionId, Guid userId, Guid examTypeId, CancellationToken cancellationToken = default);
    }
}
