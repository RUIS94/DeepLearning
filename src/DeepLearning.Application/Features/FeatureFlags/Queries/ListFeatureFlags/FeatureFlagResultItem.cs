namespace DeepLearning.Application.Features.FeatureFlags.Queries.ListFeatureFlags
{
    /// <param name="Enabled">Effective value — the row's value, or the documented default when no row exists.</param>
    /// <param name="HasRow">False = no <c>feature_flags</c> row yet, the value shown is the code default.</param>
    public record FeatureFlagResultItem(string Key, bool Enabled, string Scope, bool HasRow);
}
