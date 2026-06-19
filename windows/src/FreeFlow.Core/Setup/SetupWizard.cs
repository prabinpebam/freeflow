using FreeFlow.Core.Settings;

namespace FreeFlow.Core.Setup;

/// <summary>Ordered steps of the first-run setup wizard.</summary>
public enum SetupStep
{
    Welcome,
    Providers,
    Shortcuts,
    Microphone,
    Finish,
}

/// <summary>
/// Mutable-by-copy state the wizard threads through its steps: the settings being
/// assembled plus a flag recording that the user acknowledged the microphone
/// permission prompt.
/// </summary>
public sealed record SetupWizardState(AppSettings Settings, bool MicrophoneAcknowledged = false);

/// <summary>
/// Pure state machine for the first-run wizard. It decides which step is reachable
/// next and whether setup can complete, based solely on validated settings — no UI
/// or OS dependency — so the flow is asserted in the deterministic inner loop.
/// </summary>
public static class SetupWizard
{
    public static readonly IReadOnlyList<SetupStep> Steps = new[]
    {
        SetupStep.Welcome,
        SetupStep.Providers,
        SetupStep.Shortcuts,
        SetupStep.Microphone,
        SetupStep.Finish,
    };

    /// <summary>Whether the given step's requirements are satisfied by the state.</summary>
    public static bool IsStepComplete(SetupStep step, SetupWizardState state)
    {
        var validation = SettingsValidator.Validate(state.Settings);

        return step switch
        {
            SetupStep.Welcome => true,
            SetupStep.Providers => !HasErrorFor(validation, "Providers")
                && !string.IsNullOrWhiteSpace(state.Settings.Providers.Transcription.ApiKey),
            SetupStep.Shortcuts => !HasErrorFor(validation, "Hotkeys"),
            SetupStep.Microphone => state.MicrophoneAcknowledged,
            SetupStep.Finish => CanFinish(state),
            _ => false,
        };
    }

    /// <summary>The next step the user may advance to, or null when on the last step.</summary>
    public static SetupStep? NextStep(SetupStep current)
    {
        var index = Steps.ToList().IndexOf(current);
        return index >= 0 && index < Steps.Count - 1 ? Steps[index + 1] : null;
    }

    /// <summary>
    /// True when every prerequisite step is complete: provider config valid with a
    /// key, shortcuts non-conflicting, and microphone acknowledged.
    /// </summary>
    public static bool CanFinish(SetupWizardState state)
        => IsStepComplete(SetupStep.Providers, state)
            && IsStepComplete(SetupStep.Shortcuts, state)
            && IsStepComplete(SetupStep.Microphone, state);

    private static bool HasErrorFor(ValidationResult result, string fieldPrefix)
        => result.Issues.Any(i =>
            i.Severity == ValidationSeverity.Error &&
            i.Field.StartsWith(fieldPrefix, StringComparison.Ordinal));
}
