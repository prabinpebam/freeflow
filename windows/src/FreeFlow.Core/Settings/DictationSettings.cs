namespace FreeFlow.Core.Settings;

/// <summary>
/// Subset of user settings that influence pipeline behavior. Expanded as
/// parity rows land; kept platform-agnostic.
/// </summary>
public sealed record DictationSettings
{
    public string CustomVocabulary { get; init; } = string.Empty;
    public string SystemPrompt { get; init; } = string.Empty;
    public string ContextSystemPrompt { get; init; } = string.Empty;
    public bool PostProcessingEnabled { get; init; } = true;

    /// <summary>
    /// Honor a spoken "press enter" at the end of dictation by submitting a
    /// Return keystroke after pasting (parity with the macOS press-enter command).
    /// </summary>
    public bool PressEnterEnabled { get; init; } = true;

    /// <summary>Optional target language for translation output (parity: output language).</summary>
    public string? OutputLanguage { get; init; }

    /// <summary>
    /// User-defined voice macros. When the raw transcript matches a macro command
    /// (loose comparison), the macro payload is pasted verbatim and transcription
    /// cleanup is bypassed (parity with the macOS voice macros).
    /// </summary>
    public IReadOnlyList<Macros.VoiceMacro> VoiceMacros { get; init; } = System.Array.Empty<Macros.VoiceMacro>();

    /// <summary>
    /// Edit Mode (command mode) preferences. When enabled, dictating over selected
    /// text transforms the selection instead of inserting new text (parity with the
    /// macOS command mode).
    /// </summary>
    public EditModeSettings EditMode { get; init; } = new();
}
