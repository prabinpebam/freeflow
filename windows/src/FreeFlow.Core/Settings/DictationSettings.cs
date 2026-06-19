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
}
