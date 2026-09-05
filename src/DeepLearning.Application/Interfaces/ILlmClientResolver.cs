using DeepLearning.Domain.Enums;

namespace DeepLearning.Application.Interfaces
{
    /// <summary>
    /// Looks up the provider to use for one AI call and hands back an ILlmClient pre-configured
    /// with that provider's Model/ThinkingEnabled/Effort/ExtraSettings as defaults (callers may
    /// still override any of these per-call by setting them explicitly on the request). Callers
    /// use it instead of injecting ILlmClient directly.
    ///
    /// <para>Resolution order: an <c>ai_operation_provider_overrides</c> row for
    /// <paramref name="operationType"/> wins if one exists — that operation always runs through
    /// its pinned provider regardless of which provider is globally active. Otherwise it falls
    /// back to whichever provider is <c>llm_provider_settings.is_active = true</c> (database, not
    /// config — switching either takes effect on the next call, no redeploy).</para>
    /// </summary>
    public interface ILlmClientResolver
    {
        Task<ILlmClient> GetActiveClientAsync(AiOperationType operationType, CancellationToken cancellationToken = default);
    }
}
