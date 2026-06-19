using System;
using System.Linq;
using FreeFlow.Core.Settings;
using FreeFlow.Core.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FreeFlow_App;

/// <summary>
/// First-run setup wizard. A thin UI shell over the tested Core
/// <see cref="SetupWizard"/> state machine: it gates Next/Finish on the same
/// rules asserted in the inner loop (provider key present, shortcuts valid,
/// microphone acknowledged). On finish it persists the provider settings and
/// restarts the app so the new credentials take effect on the running pipeline.
/// </summary>
public sealed partial class OnboardingPage : Page
{
    private readonly ISettingsStore _store;
    private AppSettings _settings = AppSettings.Defaults;
    private int _index;

    public OnboardingPage()
    {
        InitializeComponent();
        _store = App.Services.GetRequiredService<ISettingsStore>();
    }

    private SetupStep CurrentStep => SetupWizard.Steps[_index];

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _settings = _store.Load();

        TranscriptionBaseUrl.Text = _settings.Providers.Transcription.BaseUrl;
        TranscriptionApiKey.Password = _settings.Providers.Transcription.ApiKey;
        TranscriptionModel.Text = _settings.Providers.Transcription.Model;
        TranscriptionApiVersion.Text = _settings.Providers.Transcription.ApiVersion;

        ShortcutHold.Text = $"Hold-to-talk:  {_settings.Hotkeys.HoldToTalk.Format()}";
        ShortcutToggle.Text = $"Toggle:  {_settings.Hotkeys.Toggle.Format()}";
        ShortcutPasteAgain.Text = $"Paste again:  {_settings.Hotkeys.PasteAgain.Format()}";

        ShowStep(0);
    }

    private void ShowStep(int index)
    {
        _index = Math.Clamp(index, 0, SetupWizard.Steps.Count - 1);
        StepInfo.IsOpen = false;

        WelcomePanel.Visibility = Vis(SetupStep.Welcome);
        ProvidersPanel.Visibility = Vis(SetupStep.Providers);
        ShortcutsPanel.Visibility = Vis(SetupStep.Shortcuts);
        MicrophonePanel.Visibility = Vis(SetupStep.Microphone);
        FinishPanel.Visibility = Vis(SetupStep.Finish);

        StepTitle.Text = CurrentStep switch
        {
            SetupStep.Welcome => "Welcome to FreeFlow",
            SetupStep.Providers => "Connect your transcription provider",
            SetupStep.Shortcuts => "Your shortcuts",
            SetupStep.Microphone => "Microphone access",
            SetupStep.Finish => "All set",
            _ => "Setup",
        };
        StepProgress.Text = $"Step {_index + 1} of {SetupWizard.Steps.Count}";

        BackButton.IsEnabled = _index > 0;
        NextButton.Content = CurrentStep == SetupStep.Finish ? "Finish & restart" : "Next";
    }

    private Visibility Vis(SetupStep step) => CurrentStep == step ? Visibility.Visible : Visibility.Collapsed;

    private SetupWizardState BuildState()
    {
        var settings = _settings with
        {
            Providers = _settings.Providers with
            {
                Transcription = _settings.Providers.Transcription with
                {
                    BaseUrl = TranscriptionBaseUrl.Text.Trim(),
                    ApiKey = TranscriptionApiKey.Password,
                    Model = TranscriptionModel.Text.Trim(),
                    ApiVersion = TranscriptionApiVersion.Text.Trim(),
                },
            },
        };

        return new SetupWizardState(settings, MicAcknowledged.IsChecked == true);
    }

    private void OnMicAckChanged(object sender, RoutedEventArgs e) => StepInfo.IsOpen = false;

    private void OnBackClick(object sender, RoutedEventArgs e) => ShowStep(_index - 1);

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        var state = BuildState();

        // Gate advancing on the same rules the Core state machine enforces.
        if (CurrentStep != SetupStep.Welcome
            && CurrentStep != SetupStep.Finish
            && !SetupWizard.IsStepComplete(CurrentStep, state))
        {
            StepInfo.Severity = InfoBarSeverity.Warning;
            StepInfo.Title = CurrentStep switch
            {
                SetupStep.Providers => "Enter your endpoint URL, API key and model to continue.",
                SetupStep.Shortcuts => "Your shortcuts conflict — adjust them in Settings.",
                SetupStep.Microphone => "Please acknowledge microphone access to continue.",
                _ => "Please complete this step to continue.",
            };
            StepInfo.IsOpen = true;
            return;
        }

        if (CurrentStep == SetupStep.Finish)
        {
            Finish(state);
            return;
        }

        ShowStep(_index + 1);
    }

    private void OnSkipClick(object sender, RoutedEventArgs e)
    {
        // Persist the onboarding-complete flag (and any provider details typed so
        // far) so the wizard does not reappear, then continue into the app.
        var settings = BuildState().Settings with
        {
            General = _settings.General with { HasCompletedOnboarding = true },
        };
        TrySave(settings);
        Frame.Navigate(typeof(MainPage));
    }

    private void Finish(SetupWizardState state)
    {
        var settings = state.Settings with
        {
            General = _settings.General with { HasCompletedOnboarding = true },
        };
        TrySave(settings);

        // Restart so the freshly-entered provider credentials are picked up by the
        // pipeline (providers are wired at composition time). Fall back to simply
        // entering the app if restart is unavailable.
        try
        {
            Microsoft.Windows.AppLifecycle.AppInstance.Restart(string.Empty);
        }
        catch
        {
            Frame.Navigate(typeof(MainPage));
        }
    }

    private void TrySave(AppSettings settings)
    {
        try
        {
            _store.Save(settings);
        }
        catch
        {
            // Best effort: even if persistence fails the user can configure later
            // in Settings; never block leaving onboarding.
        }
    }
}
