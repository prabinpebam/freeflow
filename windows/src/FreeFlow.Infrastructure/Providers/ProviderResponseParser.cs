using System.Text.Json;

namespace FreeFlow.Infrastructure.Providers;

/// <summary>
/// Pure parsing of OpenAI-compatible provider responses. Kept separate from the
/// HTTP transport so the (fragile) JSON-shape handling is asserted with contract
/// oracles in the deterministic tiers, with no network involved.
/// </summary>
public static class ProviderResponseParser
{
    /// <summary>
    /// Extract the transcript from a transcription response. Accepts the
    /// canonical <c>{ "text": "..." }</c> shape and the verbose/segment shape.
    /// </summary>
    public static string ParseTranscription(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("text", out var text) &&
            text.ValueKind == JsonValueKind.String)
        {
            return text.GetString()?.Trim() ?? string.Empty;
        }

        // Verbose JSON: concatenate segment texts.
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("segments", out var segments) &&
            segments.ValueKind == JsonValueKind.Array)
        {
            var parts = new List<string>();
            foreach (var seg in segments.EnumerateArray())
            {
                if (seg.TryGetProperty("text", out var segText) && segText.ValueKind == JsonValueKind.String)
                {
                    parts.Add(segText.GetString() ?? string.Empty);
                }
            }

            return string.Concat(parts).Trim();
        }

        throw new FormatException("Transcription response did not contain a 'text' field.");
    }

    /// <summary>
    /// Extract the assistant message content from a chat-completions response
    /// (<c>choices[0].message.content</c>).
    /// </summary>
    public static string ParseChatCompletion(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("choices", out var choices) &&
            choices.ValueKind == JsonValueKind.Array &&
            choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            if (first.TryGetProperty("message", out var message) &&
                message.TryGetProperty("content", out var content) &&
                content.ValueKind == JsonValueKind.String)
            {
                return content.GetString()?.Trim() ?? string.Empty;
            }
        }

        throw new FormatException("Chat-completions response did not contain choices[0].message.content.");
    }

    /// <summary>
    /// Extract an error message from a provider error body, falling back to the
    /// raw payload. Used to produce actionable <see cref="ProviderException"/>s.
    /// </summary>
    public static string ParseErrorMessage(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "Provider returned an empty error body.";
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.Object &&
                    error.TryGetProperty("message", out var msg) &&
                    msg.ValueKind == JsonValueKind.String)
                {
                    return msg.GetString() ?? json;
                }

                if (error.ValueKind == JsonValueKind.String)
                {
                    return error.GetString() ?? json;
                }
            }
        }
        catch (JsonException)
        {
            // Non-JSON error body; return as-is.
        }

        return json;
    }
}
