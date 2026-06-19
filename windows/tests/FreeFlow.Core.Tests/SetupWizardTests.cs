using FluentAssertions;
using FreeFlow.Core.Providers;
using FreeFlow.Core.Settings;
using FreeFlow.Core.Setup;

namespace FreeFlow.Core.Tests;

[Trait("Tier", "L0")]
public class SetupWizardTests
{
    private static AppSettings WithKeys() => AppSettings.Defaults with
    {
        Providers = new ProviderConfiguration
        {
            Transcription = new ProviderSettings { Model = "whisper-large-v3", ApiKey = "sk-t" },
            PostProcessing = new ProviderSettings { Model = "llama-3.3-70b-versatile", ApiKey = "sk-p" },
        },
    };

    [Fact]
    public void Welcome_step_is_always_complete()
    {
        var state = new SetupWizardState(AppSettings.Defaults);
        SetupWizard.IsStepComplete(SetupStep.Welcome, state).Should().BeTrue();
    }

    [Fact]
    public void Providers_step_requires_a_transcription_key()
    {
        var withoutKey = new SetupWizardState(AppSettings.Defaults);
        SetupWizard.IsStepComplete(SetupStep.Providers, withoutKey).Should().BeFalse();

        var withKey = new SetupWizardState(WithKeys());
        SetupWizard.IsStepComplete(SetupStep.Providers, withKey).Should().BeTrue();
    }

    [Fact]
    public void Microphone_step_requires_acknowledgement()
    {
        SetupWizard.IsStepComplete(SetupStep.Microphone, new SetupWizardState(WithKeys()))
            .Should().BeFalse();
        SetupWizard.IsStepComplete(SetupStep.Microphone, new SetupWizardState(WithKeys(), true))
            .Should().BeTrue();
    }

    [Fact]
    public void Cannot_finish_until_all_prerequisites_met()
    {
        SetupWizard.CanFinish(new SetupWizardState(AppSettings.Defaults)).Should().BeFalse();
        SetupWizard.CanFinish(new SetupWizardState(WithKeys())).Should().BeFalse();
        SetupWizard.CanFinish(new SetupWizardState(WithKeys(), true)).Should().BeTrue();
    }

    [Fact]
    public void NextStep_walks_the_ordered_flow_then_stops()
    {
        SetupWizard.NextStep(SetupStep.Welcome).Should().Be(SetupStep.Providers);
        SetupWizard.NextStep(SetupStep.Microphone).Should().Be(SetupStep.Finish);
        SetupWizard.NextStep(SetupStep.Finish).Should().BeNull();
    }
}
