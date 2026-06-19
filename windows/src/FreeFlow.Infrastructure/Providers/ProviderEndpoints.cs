using FreeFlow.Core.Providers;

namespace FreeFlow.Infrastructure.Providers;

/// <summary>
/// Pure resolution of the request URL for a provider operation. Supports two
/// endpoint shapes from a single <see cref="ProviderSettings"/>:
/// <list type="bullet">
/// <item><description>
/// OpenAI-compatible (Groq, OpenAI): <c>{BaseUrl}/{operation}</c>, e.g.
/// <c>https://api.groq.com/openai/v1/audio/transcriptions</c>.
/// </description></item>
/// <item><description>
/// Azure OpenAI (when <see cref="ProviderSettings.ApiVersion"/> is set):
/// <c>{BaseUrl}/openai/deployments/{Model}/{operation}?api-version={ApiVersion}</c>,
/// e.g. <c>https://res.cognitiveservices.azure.com/openai/deployments/gpt-4o-transcribe/audio/transcriptions?api-version=2025-03-01-preview</c>.
/// </description></item>
/// </list>
/// No network I/O — deterministic so it can be asserted directly in unit tests.
/// </summary>
public static class ProviderEndpoints
{
    /// <summary>OpenAI-compatible / Azure operation path for speech-to-text.</summary>
    public const string Transcriptions = "audio/transcriptions";

    /// <summary>OpenAI-compatible / Azure operation path for chat completions.</summary>
    public const string ChatCompletions = "chat/completions";

    /// <summary>
    /// Builds the request URI for <paramref name="operation"/> against the given
    /// provider, choosing the Azure or OpenAI-compatible URL shape automatically.
    /// </summary>
    public static Uri Resolve(ProviderSettings settings, string operation)
    {
        if (settings is null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        if (string.IsNullOrWhiteSpace(operation))
        {
            throw new ArgumentException("Operation path is required.", nameof(operation));
        }

        var baseUrl = settings.BaseUrl.TrimEnd('/');
        var op = operation.TrimStart('/');

        if (!settings.IsAzure)
        {
            return new Uri(new Uri(baseUrl + "/"), op);
        }

        if (string.IsNullOrWhiteSpace(settings.Model))
        {
            throw new ArgumentException(
                "Azure OpenAI requires a deployment name (Model).", nameof(settings));
        }

        var deployment = Uri.EscapeDataString(settings.Model);
        var apiVersion = Uri.EscapeDataString(settings.ApiVersion);
        return new Uri($"{baseUrl}/openai/deployments/{deployment}/{op}?api-version={apiVersion}");
    }
}
