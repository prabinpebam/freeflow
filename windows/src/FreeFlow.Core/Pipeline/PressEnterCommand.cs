using System.Text.RegularExpressions;

namespace FreeFlow.Core.Pipeline;

/// <summary>
/// Detects (and strips) a trailing spoken "press enter" command from a
/// transcript, mirroring the macOS feature: saying "…send the message, press
/// enter" pastes the text and then synthesizes a Return key so the host app
/// submits. Pure and deterministic; the actual keystroke is sent via a platform
/// seam after the cleaned text is pasted.
/// </summary>
public static partial class PressEnterCommand
{
    // Matches a trailing "press enter" preceded by start/whitespace/punctuation,
    // tolerating trailing punctuation/whitespace. Ported from the macOS regex.
    [GeneratedRegex(@"(?:^|[ \t\r\n,;:\-]+)press[ \t\r\n]+enter[\s\p{P}]*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TrailingPressEnter();

    /// <summary>
    /// Returns the transcript with any trailing "press enter" command removed and
    /// whether such a command was present.
    /// </summary>
    public static (string Text, bool PressEnter) Detect(string? transcript)
    {
        if (string.IsNullOrEmpty(transcript))
        {
            return (transcript ?? string.Empty, false);
        }

        var match = TrailingPressEnter().Match(transcript);
        if (!match.Success)
        {
            return (transcript, false);
        }

        var cleaned = transcript[..match.Index].TrimEnd();
        return (cleaned, true);
    }
}
