using System.Collections.Generic;
using System.Text;

namespace FreeFlow.Core.Macros;

/// <summary>
/// Pure matcher that resolves a raw transcript to a voice-macro payload. Mirrors
/// the macOS normalization (lowercase, strip punctuation, trim whitespace) and
/// the whole-transcript exact-match rule: a macro fires only when the entire
/// spoken phrase equals the macro command after normalization.
/// </summary>
public static class VoiceMacroMatcher
{
    /// <summary>
    /// Returns the matching macro for <paramref name="transcript"/>, or
    /// <c>null</c> when no macro matches. The first macro (in list order) whose
    /// normalized command equals the normalized transcript wins.
    /// </summary>
    public static VoiceMacro? Match(string transcript, IReadOnlyList<VoiceMacro>? macros)
    {
        if (macros is null || macros.Count == 0)
        {
            return null;
        }

        var normalizedTranscript = Normalize(transcript);
        if (normalizedTranscript.Length == 0)
        {
            return null;
        }

        foreach (var macro in macros)
        {
            if (Normalize(macro.Command) == normalizedTranscript)
            {
                return macro;
            }
        }

        return null;
    }

    /// <summary>
    /// Normalizes text for loose comparison: lowercase (invariant), all Unicode
    /// punctuation removed, surrounding whitespace trimmed.
    /// </summary>
    public static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var lowered = text.ToLowerInvariant();
        var sb = new StringBuilder(lowered.Length);
        foreach (var ch in lowered)
        {
            if (!char.IsPunctuation(ch))
            {
                sb.Append(ch);
            }
        }

        return sb.ToString().Trim();
    }
}
