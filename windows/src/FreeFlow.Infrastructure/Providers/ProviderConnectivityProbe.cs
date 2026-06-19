using System.Net.Http.Headers;
using System.Text;
using FreeFlow.Core.Audio;
using FreeFlow.Core.Providers;
using FreeFlow.Infrastructure.Audio;

namespace FreeFlow.Infrastructure.Providers;

/// <summary>Outcome of a single live provider connectivity check.</summary>
/// <param name="Success">True when the provider returned a 2xx response.</param>
/// <param name="StatusCode">HTTP status code, when a response was received.</param>
/// <param name="Message">Human-readable, actionable summary for the settings UI.</param>
public sealed record ProviderCheckResult(bool Success, int? StatusCode, string Message);

/// <summary>
/// Makes a <b>real</b>, low-cost API call to a provider endpoint to verify the
/// Base URL, API key, model/deployment, and (for Azure) API version actually
/// work end-to-end — not just that they pass static validation. Reuses the same
/// request shaping (<see cref="OpenAiCompatibleRequestFactory"/>), URL resolution
/// (<see cref="ProviderEndpoints"/>), and error parsing
/// (<see cref="ProviderResponseParser"/>) as the live dictation clients, so a
/// successful probe means the configured pipeline will work. The HTTP transport
/// is the only impure part and is exercised in tests via a stub handler.
/// </summary>
public sealed class ProviderConnectivityProbe
{
    // ~0.1s of PCM16 silence at 16 kHz mono. Providers accept silence and return
    // 200 with empty text, which is all the probe needs to confirm credentials.
    private static readonly AudioClip ProbeClip = new(new byte[3200]);

    private readonly HttpClient _http;

    public ProviderConnectivityProbe(HttpClient http) => _http = http;

    /// <summary>Verify the transcription endpoint by posting a tiny audio clip.</summary>
    public Task<ProviderCheckResult> CheckTranscriptionAsync(
        ProviderSettings settings, CancellationToken ct = default)
    {
        return SendAsync("Transcription", () =>
        {
            var fields = OpenAiCompatibleRequestFactory.BuildTranscriptionFields(settings.Model);
            var content = new MultipartFormDataContent();
            foreach (var field in fields)
            {
                content.Add(new StringContent(field.Value), field.Key);
            }

            var wav = WavEncoder.Encode(ProbeClip);
            var fileContent = new ByteArrayContent(wav);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            content.Add(fileContent, "file", "audio.wav");

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                ProviderEndpoints.Resolve(settings, ProviderEndpoints.Transcriptions))
            {
                Content = content,
            };
            Authorize(request, settings);
            return request;
        }, settings, ct);
    }

    /// <summary>Verify the chat-completions endpoint with a minimal prompt.</summary>
    public Task<ProviderCheckResult> CheckPostProcessingAsync(
        ProviderSettings settings, CancellationToken ct = default)
    {
        return SendAsync("Post-processing", () =>
        {
            var body = OpenAiCompatibleRequestFactory.BuildChatCompletionsBody(
                settings.Model,
                systemPrompt: "Connectivity check. Reply with OK.",
                userContent: "ping");

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                ProviderEndpoints.Resolve(settings, ProviderEndpoints.ChatCompletions))
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            Authorize(request, settings);
            return request;
        }, settings, ct);
    }

    private async Task<ProviderCheckResult> SendAsync(
        string label, Func<HttpRequestMessage> build, ProviderSettings settings, CancellationToken ct)
    {
        try
        {
            using var request = build();
            using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var code = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                return new ProviderCheckResult(
                    true, code, $"{label}: reached \"{settings.Model}\" and credentials accepted (HTTP {code}).");
            }

            var detail = ProviderResponseParser.ParseErrorMessage(body);
            var hint = code switch
            {
                401 or 403 => "check the API key",
                404 => "check the Base URL and model/deployment name",
                429 => "rate limited — try again shortly",
                _ => "see the provider's response above",
            };
            return new ProviderCheckResult(false, code, $"{label}: HTTP {code} — {detail} ({hint}).");
        }
        catch (OperationCanceledException)
        {
            return new ProviderCheckResult(false, null, $"{label}: request timed out or was canceled.");
        }
        catch (HttpRequestException ex)
        {
            return new ProviderCheckResult(false, null, $"{label}: could not reach the endpoint — {ex.Message}");
        }
        catch (Exception ex)
        {
            return new ProviderCheckResult(false, null, $"{label}: {ex.Message}");
        }
    }

    private static void Authorize(HttpRequestMessage request, ProviderSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        }
    }
}
