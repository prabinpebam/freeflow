using System.Net.Http.Headers;
using FreeFlow.Core.Abstractions;
using FreeFlow.Core.Audio;
using FreeFlow.Core.Pipeline;
using FreeFlow.Core.Providers;
using FreeFlow.Infrastructure.Audio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FreeFlow.Infrastructure.Providers;

/// <summary>
/// Real OpenAI-compatible speech-to-text client. Shapes the multipart request
/// with <see cref="OpenAiCompatibleRequestFactory"/>, posts the WAV-encoded clip
/// over an injected <see cref="HttpClient"/>, and parses the transcript with
/// <see cref="ProviderResponseParser"/>. The transport is the only non-pure part;
/// request shaping and response parsing are independently unit-tested.
/// </summary>
public sealed class HttpTranscriptionClient : ITranscriptionClient
{
    private readonly HttpClient _http;
    private readonly ProviderSettings _settings;
    private readonly ILogger<HttpTranscriptionClient> _logger;

    public HttpTranscriptionClient(
        HttpClient http,
        ProviderSettings settings,
        ILogger<HttpTranscriptionClient>? logger = null)
    {
        _http = http;
        _settings = settings;
        _logger = logger ?? NullLogger<HttpTranscriptionClient>.Instance;
    }

    public async Task<string> TranscribeAsync(AudioClip clip, TranscriptionOptions options, CancellationToken ct = default)
    {
        if (clip.IsEmpty)
        {
            return string.Empty;
        }

        var prompt = string.IsNullOrWhiteSpace(options.CustomVocabulary) ? null : options.CustomVocabulary;
        var fields = OpenAiCompatibleRequestFactory.BuildTranscriptionFields(
            _settings.Model,
            language: null,
            prompt: prompt);

        using var content = new MultipartFormDataContent();
        foreach (var field in fields)
        {
            content.Add(new StringContent(field.Value), field.Key);
        }

        var wav = WavEncoder.Encode(clip);
        var fileContent = new ByteArrayContent(wav);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(fileContent, "file", "audio.wav");

        using var request = new HttpRequestMessage(HttpMethod.Post, ProviderEndpoints.Resolve(_settings, ProviderEndpoints.Transcriptions))
        {
            Content = content,
        };
        Authorize(request);

        _logger.LogDebug("POST {Url} (model={Model}, bytes={Bytes}).", request.RequestUri, _settings.Model, wav.Length);
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new ProviderException(
                ProviderResponseParser.ParseErrorMessage(body),
                (int)response.StatusCode);
        }

        return ProviderResponseParser.ParseTranscription(body);
    }

    private void Authorize(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        }
    }
}
