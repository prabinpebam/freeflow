using System.Text.Json;

namespace FreeFlow.Infrastructure.Providers;

/// <summary>
/// Pure request shaping for OpenAI-compatible STT and chat-completions
/// endpoints (Groq by default). No network I/O — deterministic so it can be
/// asserted with snapshot/contract oracles.
/// </summary>
public static class OpenAiCompatibleRequestFactory
{
    private static readonly JsonSerializerOptions BodyOptions = new()
    {
        WriteIndented = false,
    };

    /// <summary>
    /// Build the multipart form fields for an audio transcription request
    /// (the file part is added separately by the transport).
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, string>> BuildTranscriptionFields(
        string model,
        string? language = null,
        string? prompt = null,
        string responseFormat = "json")
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("Model is required.", nameof(model));
        }

        var fields = new List<KeyValuePair<string, string>>
        {
            new("model", model),
            new("response_format", responseFormat),
        };

        if (!string.IsNullOrWhiteSpace(language))
        {
            fields.Add(new("language", language!));
        }

        if (!string.IsNullOrWhiteSpace(prompt))
        {
            fields.Add(new("prompt", prompt!));
        }

        return fields;
    }

    /// <summary>Build a deterministic chat-completions JSON body for cleanup.</summary>
    public static string BuildChatCompletionsBody(string model, string systemPrompt, string userContent)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("Model is required.", nameof(model));
        }

        var payload = new
        {
            model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userContent },
            },
            temperature = 0,
        };

        return JsonSerializer.Serialize(payload, BodyOptions);
    }
}
