namespace DeepLearning.Infrastructure.Ai.Options
{
    public class ClaudeApiOptions
    {
        public const string SectionName = "Llm:Claude";

        public string ApiKey { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://api.anthropic.com";
        public string Model { get; set; } = "claude-opus-5";
        public string ApiVersion { get; set; } = "2023-06-01";

        /// <summary>
        /// Required for identity-linked API keys ("anthropic-workspace-id is required when
        /// authenticating with an identity-linked API key" — hit for real on 2026-08-29).
        /// Optional otherwise; leave null/empty and no header is sent.
        /// </summary>
        public string? WorkspaceId { get; set; }
    }
}
