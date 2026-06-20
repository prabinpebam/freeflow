using FreeFlow.Core.Input;
using FreeFlow.Core.Providers;

namespace FreeFlow.Core.Settings;

/// <summary>Severity of a settings validation finding.</summary>
public enum ValidationSeverity
{
    /// <summary>Blocks saving / first dictation until fixed.</summary>
    Error,

    /// <summary>Allowed, but the user should be told (e.g. missing API key).</summary>
    Warning,
}

/// <summary>A single validation finding tied to a settings field.</summary>
public sealed record ValidationIssue(string Field, ValidationSeverity Severity, string Message);

/// <summary>Aggregated validation findings for a settings snapshot.</summary>
public sealed record ValidationResult(IReadOnlyList<ValidationIssue> Issues)
{
    public static readonly ValidationResult Ok = new(Array.Empty<ValidationIssue>());

    public bool HasErrors => Issues.Any(i => i.Severity == ValidationSeverity.Error);

    public bool HasWarnings => Issues.Any(i => i.Severity == ValidationSeverity.Warning);

    /// <summary>True when there are no error-severity findings (warnings are allowed).</summary>
    public bool IsValid => !HasErrors;
}

/// <summary>
/// Pure validation of <see cref="AppSettings"/>: provider endpoints/models/keys,
/// request timeouts, history cap, and global shortcut sanity (parseable and
/// non-conflicting). Returns structured findings so both the settings UI and the
/// setup wizard can render them and the inner loop can assert them.
/// </summary>
public static class SettingsValidator
{
    public static ValidationResult Validate(AppSettings settings)
    {
        var issues = new List<ValidationIssue>();

        ValidateProvider(issues, "Transcription", settings.Providers.Transcription, required: true);

        // The post-processing endpoint only matters when cleanup is enabled.
        if (settings.Dictation.PostProcessingEnabled)
        {
            ValidateProvider(issues, "PostProcessing", settings.Providers.PostProcessing, required: true);

            if (string.IsNullOrWhiteSpace(settings.Providers.PostProcessingFallbackModel))
            {
                issues.Add(new ValidationIssue(
                    "Providers.PostProcessingFallbackModel",
                    ValidationSeverity.Warning,
                    "No fallback model set; cleanup failures will surface the raw transcript."));
            }
        }

        if (settings.General.HistoryCap < 1)
        {
            issues.Add(new ValidationIssue(
                "General.HistoryCap",
                ValidationSeverity.Error,
                "History cap must be at least 1."));
        }

        ValidateHotkeys(issues, settings.Hotkeys);
        ValidateEditMode(issues, settings.Dictation.EditMode, settings.Hotkeys);

        return issues.Count == 0 ? ValidationResult.Ok : new ValidationResult(issues);
    }

    private static void ValidateProvider(
        List<ValidationIssue> issues, string name, ProviderSettings provider, bool required)
    {
        var field = $"Providers.{name}";

        if (!IsAbsoluteHttpUrl(provider.BaseUrl))
        {
            issues.Add(new ValidationIssue(
                $"{field}.BaseUrl",
                ValidationSeverity.Error,
                "Base URL must be an absolute http(s) URL."));
        }

        if (string.IsNullOrWhiteSpace(provider.Model))
        {
            issues.Add(new ValidationIssue(
                $"{field}.Model",
                ValidationSeverity.Error,
                "Model id is required."));
        }

        if (provider.Timeout <= TimeSpan.Zero)
        {
            issues.Add(new ValidationIssue(
                $"{field}.Timeout",
                ValidationSeverity.Error,
                "Timeout must be greater than zero."));
        }

        if (required && string.IsNullOrWhiteSpace(provider.ApiKey))
        {
            issues.Add(new ValidationIssue(
                $"{field}.ApiKey",
                ValidationSeverity.Warning,
                "API key is required before the first dictation."));
        }
    }

    private static void ValidateHotkeys(List<ValidationIssue> issues, HotkeyBindings hotkeys)
    {
        var named = new (string Name, HotkeyCombination Combo)[]
        {
            ("HoldToTalk", hotkeys.HoldToTalk),
            ("Toggle", hotkeys.Toggle),
            ("PasteAgain", hotkeys.PasteAgain),
        };

        foreach (var (n, combo) in named)
        {
            if (combo.IsUnset)
            {
                issues.Add(new ValidationIssue(
                    $"Hotkeys.{n}",
                    ValidationSeverity.Error,
                    "Shortcut is not set."));
            }
        }

        foreach (var conflict in HotkeyCapture.FindConflicts(hotkeys))
        {
            issues.Add(new ValidationIssue(
                "Hotkeys",
                ValidationSeverity.Error,
                conflict));
        }
    }

    /// <summary>
    /// When manual Edit Mode is enabled, its extra modifier must stay distinct from
    /// the dictation shortcuts — otherwise pressing a shortcut would always look
    /// like an Edit Mode request (parity with the macOS collision checks).
    /// </summary>
    private static void ValidateEditMode(
        List<ValidationIssue> issues, EditModeSettings editMode, HotkeyBindings hotkeys)
    {
        if (!editMode.Enabled || editMode.Style != CommandModeStyle.Manual)
        {
            return;
        }

        var modifier = editMode.ManualModifier;
        var named = new (string Name, HotkeyCombination Combo)[]
        {
            ("hold-to-talk", hotkeys.HoldToTalk),
            ("toggle", hotkeys.Toggle),
            ("paste-again", hotkeys.PasteAgain),
        };

        foreach (var (n, combo) in named)
        {
            if (modifier.Collides(combo))
            {
                issues.Add(new ValidationIssue(
                    "Dictation.EditMode.ManualModifier",
                    ValidationSeverity.Error,
                    $"The Edit Mode modifier ({modifier.Title()}) is already part of the {n} shortcut."));
            }
        }
    }

    private static bool IsAbsoluteHttpUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
