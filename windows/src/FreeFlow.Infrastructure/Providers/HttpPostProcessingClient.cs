using System.Net.Http.Headers;
using System.Text;
using FreeFlow.Core.Abstractions;
using FreeFlow.Core.Context;
using FreeFlow.Core.Pipeline;
using FreeFlow.Core.Providers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FreeFlow.Infrastructure.Providers;

/// <summary>
/// Real OpenAI-compatible chat-completions client used for transcript cleanup.
/// Builds a deterministic request body with
/// <see cref="OpenAiCompatibleRequestFactory"/>, posts it over an injected
/// <see cref="HttpClient"/>, and extracts the cleaned text. Honors the cleanup
/// contract: if the model returns empty for non-empty input, the raw transcript
/// is returned unchanged so the user never loses their words.
/// </summary>
public sealed class HttpPostProcessingClient : IPostProcessingClient
{
    private readonly HttpClient _http;
    private readonly ProviderSettings _settings;
    private readonly ILogger<HttpPostProcessingClient> _logger;

    public HttpPostProcessingClient(
        HttpClient http,
        ProviderSettings settings,
        ILogger<HttpPostProcessingClient>? logger = null)
    {
        _http = http;
        _settings = settings;
        _logger = logger ?? NullLogger<HttpPostProcessingClient>.Instance;
    }

    public async Task<string> CleanupAsync(PostProcessingRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RawTranscript))
        {
            return string.Empty;
        }

        var systemPrompt = BuildSystemPrompt(request);
        var body = OpenAiCompatibleRequestFactory.BuildChatCompletionsBody(
            _settings.Model,
            systemPrompt,
            request.RawTranscript);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, HttpTranscriptionClient.Combine(_settings.BaseUrl, "chat/completions"))
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }

        _logger.LogDebug("POST {Url} (model={Model}).", httpRequest.RequestUri, _settings.Model);
        using var response = await _http.SendAsync(httpRequest, ct).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new ProviderException(
                ProviderResponseParser.ParseErrorMessage(responseBody),
                (int)response.StatusCode);
        }

        var cleaned = ProviderResponseParser.ParseChatCompletion(responseBody);

        // Cleanup contract: never blank out a non-empty transcript.
        return string.IsNullOrWhiteSpace(cleaned) ? request.RawTranscript : cleaned;
    }

    private static string BuildSystemPrompt(PostProcessingRequest request)
    {
        var sb = new StringBuilder();
        sb.Append(string.IsNullOrWhiteSpace(request.Settings.SystemPrompt)
            ? "You clean up dictated text. Remove filler words and fix punctuation and capitalization. "
              + "Preserve the speaker's meaning and wording. Never invent names or facts. "
              + "Return only the cleaned text with no preamble."
            : request.Settings.SystemPrompt);

        if (!string.IsNullOrWhiteSpace(request.Settings.CustomVocabulary))
        {
            sb.Append("\nPreserve these terms exactly: ").Append(request.Settings.CustomVocabulary).Append('.');
        }

        if (!string.IsNullOrWhiteSpace(request.Settings.OutputLanguage))
        {
            sb.Append("\nTranslate the output into ").Append(request.Settings.OutputLanguage).Append('.');
        }

        AppendContext(sb, request.Context);
        return sb.ToString();
    }

    private static void AppendContext(StringBuilder sb, CaptureContext context)
    {
        if (context is null || context == CaptureContext.None)
        {
            return;
        }

        var app = context.AppName ?? context.ProcessName;
        if (!string.IsNullOrWhiteSpace(app))
        {
            sb.Append("\nThe user is dictating into ").Append(app);
            if (!string.IsNullOrWhiteSpace(context.WindowTitle))
            {
                sb.Append(" (").Append(context.WindowTitle).Append(')');
            }

            sb.Append('.');
        }
    }
}
