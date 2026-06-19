namespace FreeFlow.Core.Providers;

/// <summary>
/// Connection settings for one OpenAI-compatible endpoint (Groq by default).
/// Shared shape for the transcription and chat-completions providers.
/// </summary>
public sealed record ProviderSettings
{
    /// <summary>Base URL, e.g. <c>https://api.groq.com/openai/v1</c>.</summary>
    public string BaseUrl { get; init; } = "https://api.groq.com/openai/v1";

    /// <summary>Bearer API key. Stored encrypted at rest (DPAPI) outside Core.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>Model id for this endpoint.</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// Azure OpenAI API version, e.g. <c>2025-03-01-preview</c>. When set, the
    /// provider is treated as Azure OpenAI: requests are routed to
    /// <c>{BaseUrl}/openai/deployments/{Model}/{operation}?api-version={ApiVersion}</c>
    /// (with <see cref="Model"/> used as the deployment name). Leave empty for a
    /// standard OpenAI-compatible endpoint such as Groq.
    /// </summary>
    public string ApiVersion { get; init; } = string.Empty;

    /// <summary>True when this endpoint targets Azure OpenAI (an API version is set).</summary>
    public bool IsAzure => !string.IsNullOrWhiteSpace(ApiVersion);

    /// <summary>Per-request timeout.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    public bool HasCredentials => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(ApiKey);
}

/// <summary>The two providers the dictation pipeline talks to.</summary>
public sealed record ProviderConfiguration
{
    public ProviderSettings Transcription { get; init; } = new()
    {
        Model = "whisper-large-v3",
    };

    public ProviderSettings PostProcessing { get; init; } = new()
    {
        Model = "llama-3.3-70b-versatile",
    };

    /// <summary>Optional fallback model for cleanup when the primary fails.</summary>
    public string? PostProcessingFallbackModel { get; init; } = "llama-3.1-8b-instant";
}
