namespace DeepLearning.Application.Interfaces
{
    /// <summary>
    /// Hands a grading run to a background worker so the HTTP request that asked for it can
    /// return immediately.
    ///
    /// <para>Grading is four LLM calls and takes minutes; served synchronously it outlived the
    /// request that started it every time (Node's undici, which proxies the frontend's call,
    /// gives up at 300s), so the browser saw a 500 while the server quietly finished the work
    /// and persisted a perfectly good result nobody was shown. The API now accepts the request,
    /// queues it, and answers 202; the client polls GET /submissions/{id} until the status
    /// leaves Grading.</para>
    ///
    /// <para>An interface rather than a direct Hangfire call so the API tests can run the work
    /// inline and stay deterministic — see the test project's inline implementation for what
    /// that changes about when exceptions surface.</para>
    /// </summary>
    public interface IGradingJobQueue
    {
        Task EnqueueAsync(Guid submissionId, Guid examTypeId, CancellationToken cancellationToken = default);
    }
}
