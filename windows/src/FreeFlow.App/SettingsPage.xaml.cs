using System.Linq;
using FreeFlow.Core.Input;
using FreeFlow.Core.Providers;
using FreeFlow.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FreeFlow_App;

/// <summary>
/// Settings editor backed by <see cref="ISettingsStore"/>. Loads the persisted
/// <see cref="AppSettings"/>, edits provider/shortcut/general fields, validates
/// them with the Core <see cref="SettingsValidator"/> (shared with the inner
/// loop), and saves — API keys are encrypted at rest by the store. This is the
/// thin UI shell over the fully-tested Core/Infrastructure logic.
/// </summary>
public sealed partial class SettingsPage : Page
{
    private readonly ISettingsStore _store;

    public SettingsPage()
    {
        InitializeComponent();
        _store = App.Services.GetRequiredService<ISettingsStore>();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        LoadIntoUi(_store.Load());
    }

    private void LoadIntoUi(AppSettings settings)
    {
        TranscriptionBaseUrl.Text = settings.Providers.Transcription.BaseUrl;
        TranscriptionApiKey.Password = settings.Providers.Transcription.ApiKey;
        TranscriptionModel.Text = settings.Providers.Transcription.Model;
        TranscriptionApiVersion.Text = settings.Providers.Transcription.ApiVersion;

        PostProcessingEnabled.IsOn = settings.Dictation.PostProcessingEnabled;
        PostProcessingApiKey.Password = settings.Providers.PostProcessing.ApiKey;
        PostProcessingModel.Text = settings.Providers.PostProcessing.Model;
        PostProcessingApiVersion.Text = settings.Providers.PostProcessing.ApiVersion;

        HoldToTalk.Text = settings.Hotkeys.HoldToTalk.Format();
        Toggle.Text = settings.Hotkeys.Toggle.Format();
        PasteAgain.Text = settings.Hotkeys.PasteAgain.Format();

        LaunchAtLogin.IsOn = settings.General.LaunchAtLogin;
        HistoryCap.Value = settings.General.HistoryCap;
    }

    private AppSettings BuildFromUi(AppSettings current)
    {
        return current with
        {
            Providers = current.Providers with
            {
                Transcription = current.Providers.Transcription with
                {
                    BaseUrl = TranscriptionBaseUrl.Text.Trim(),
                    ApiKey = TranscriptionApiKey.Password,
                    Model = TranscriptionModel.Text.Trim(),
                    ApiVersion = TranscriptionApiVersion.Text.Trim(),
                },
                PostProcessing = current.Providers.PostProcessing with
                {
                    ApiKey = PostProcessingApiKey.Password,
                    Model = PostProcessingModel.Text.Trim(),
                    ApiVersion = PostProcessingApiVersion.Text.Trim(),
                },
            },
            Dictation = current.Dictation with { PostProcessingEnabled = PostProcessingEnabled.IsOn },
            Hotkeys = new HotkeyBindings(
                ParseOrUnset(HoldToTalk.Text),
                ParseOrUnset(Toggle.Text),
                ParseOrUnset(PasteAgain.Text)),
            General = current.General with
            {
                LaunchAtLogin = LaunchAtLogin.IsOn,
                HistoryCap = double.IsNaN(HistoryCap.Value) ? current.General.HistoryCap : (int)HistoryCap.Value,
            },
        };
    }

    private static HotkeyCombination ParseOrUnset(string text)
        => HotkeyCombination.TryParse(text, out var combo) ? combo : HotkeyCombination.None;

    private bool TryValidate(out AppSettings settings)
    {
        settings = BuildFromUi(_store.Load());
        var result = SettingsValidator.Validate(settings);

        if (result.Issues.Count == 0)
        {
            ShowValidation(InfoBarSeverity.Success, "Settings are valid.", result);
            return true;
        }

        var severity = result.HasErrors ? InfoBarSeverity.Error : InfoBarSeverity.Warning;
        var title = result.HasErrors ? "Fix the following before saving" : "Saved-able with warnings";
        ShowValidation(severity, title, result);
        return !result.HasErrors;
    }

    private void OnValidateClick(object sender, RoutedEventArgs e) => TryValidate(out _);

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!TryValidate(out var settings))
        {
            return;
        }

        _store.Save(settings);
        ShowValidation(InfoBarSeverity.Success, "Settings saved.", SettingsValidator.Validate(settings));
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    private void ShowValidation(InfoBarSeverity severity, string title, ValidationResult result)
    {
        ValidationBar.Severity = severity;
        ValidationBar.Title = title;
        ValidationBar.Message = result.Issues.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, result.Issues.Select(i => $"• [{i.Severity}] {i.Field}: {i.Message}"));
        ValidationBar.IsOpen = true;
    }
}
