namespace FreeFlow.Core.Macros;

/// <summary>
/// A voice macro: when the spoken transcript matches <see cref="Command"/>
/// (compared loosely — case-insensitive, punctuation-stripped), the dictation is
/// replaced wholesale with <see cref="Payload"/> instead of being transcribed and
/// cleaned up. The Windows analog of the macOS <c>VoiceMacro</c>.
/// </summary>
public sealed record VoiceMacro
{
    public string Command { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
}
