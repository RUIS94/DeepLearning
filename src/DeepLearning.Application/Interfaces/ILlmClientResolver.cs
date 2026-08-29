namespace DeepLearning.Application.Interfaces
{
    /// <summary>
    /// Looks up the currently-active provider from LlmProviderSettings (database, not
    /// config — switching providers/models takes effect on the next call, no redeploy) and
    /// hands back an ILlmClient pre-configured with that row's Model/ThinkingEnabled/Effort/
    /// ExtraSettings as defaults. Callers use it instead of injecting ILlmClient directly.
    /// </summary>
    public interface ILlmClientResolver
    {
        Task<ILlmClient> GetActiveClientAsync(CancellationToken cancellationToken = default);
    }
}
