using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FreeFlow.Core.Abstractions;
using FreeFlow.Core.Audio;
using FreeFlow.Core.Input;
using FreeFlow.Core.Providers;
using FreeFlow.Core.Settings;
using FreeFlow.Infrastructure.Providers;
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
        var settings = _store.Load();
        PopulateMicrophones(settings.General.InputDeviceId);
        LoadIntoUi(settings);
    }

    private void PopulateMicrophones(string selectedId)
    {
        var provider = App.Services.GetService<IAudioDeviceProvider>();
        var devices = provider?.GetInputDevices()
            ?? new List<AudioInputDevice> { AudioInputDevice.SystemDefault };

        // Guarantee a "System default" entry, and surface a previously-saved
        // device that is no longer present so the user can see/keep their choice.
        var items = devices.ToList();
        if (!items.Any(d => d.IsSystemDefault))
        {
            items.Insert(0, AudioInputDevice.SystemDefault);
        }

        if (!string.IsNullOrEmpty(selectedId) && items.All(d => d.Id != selectedId))
        {
            items.Add(new AudioInputDevice(selectedId, $"{selectedId} (not connected)"));
        }

        MicDevice.ItemsSource = items;
        MicDevice.SelectedValue = selectedId ?? string.Empty;
        if (MicDevice.SelectedItem is null)
        {
            MicDevice.SelectedIndex = 0;
        }
    }

    private void LoadIntoUi(AppSettings settings)
    {
        TranscriptionBaseUrl.Text = settings.Providers.Transcription.BaseUrl;
        TranscriptionApiKey.Password = settings.Providers.Transcription.ApiKey;
        TranscriptionModel.Text = settings.Providers.Transcription.Model;
        TranscriptionApiVersion.Text = settings.Providers.Transcription.ApiVersion;

        PostProcessingEnabled.IsOn = settings.Dictation.PostProcessingEnabled;
        PostProcessingBaseUrl.Text = settings.Providers.PostProcessing.BaseUrl;
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
                    BaseUrl = PostProcessingBaseUrl.Text.Trim(),
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
                InputDeviceId = (MicDevice.SelectedValue as string) ?? string.Empty,
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

    private void OnValidateClick(object sender, RoutedEventArgs e) => _ = ValidateLiveAsync();

    /// <summary>
    /// "Validate" runs the static checks first, then makes a real, low-cost API
    /// call to each configured provider so the user knows their key/URL/model
    /// actually work — not just that they look well-formed.
    /// </summary>
    private async Task ValidateLiveAsync()
    {
        var settings = BuildFromUi(_store.Load());
        var staticResult = SettingsValidator.Validate(settings);

        // Static errors (bad URL, missing model, unset shortcuts) would make the
        // live call meaningless, so surface them and stop before hitting the API.
        if (staticResult.HasErrors)
        {
            ShowValidation(InfoBarSeverity.Error, "Fix the following before testing the connection", staticResult);
            return;
        }

        SetBusy(true);
        ShowMessage(InfoBarSeverity.Informational, "Testing connection…", "Calling the provider API.");
        try
        {
            var lines = new List<string>();
            var allOk = true;

            using (var http = new HttpClient { Timeout = ProbeTimeout(settings) })
            {
                var probe = new ProviderConnectivityProbe(http);

                var transcription = await probe.CheckTranscriptionAsync(settings.Providers.Transcription);
                allOk &= transcription.Success;
                lines.Add(Bullet(transcription));

                if (settings.Dictation.PostProcessingEnabled)
                {
                    var postProcessing = await probe.CheckPostProcessingAsync(settings.Providers.PostProcessing);
                    allOk &= postProcessing.Success;
                    lines.Add(Bullet(postProcessing));
                }
            }

            // Carry any non-blocking static warnings into the result too.
            foreach (var issue in staticResult.Issues)
            {
                lines.Add($"• [{issue.Severity}] {issue.Field}: {issue.Message}");
            }

            ShowMessage(
                allOk ? InfoBarSeverity.Success : InfoBarSeverity.Error,
                allOk ? "Connection successful" : "Connection failed",
                string.Join(Environment.NewLine, lines));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private static TimeSpan ProbeTimeout(AppSettings settings)
    {
        var timeout = settings.Providers.Transcription.Timeout;
        return timeout < TimeSpan.FromSeconds(10) ? TimeSpan.FromSeconds(30) : timeout;
    }

    private static string Bullet(ProviderCheckResult result)
        => $"• {(result.Success ? "✓" : "✗")} {result.Message}";

    private void SetBusy(bool busy)
    {
        ValidateButton.IsEnabled = !busy;
        SaveButton.IsEnabled = !busy;
        BackButton.IsEnabled = !busy;
    }

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
        var message = result.Issues.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, result.Issues.Select(i => $"• [{i.Severity}] {i.Field}: {i.Message}"));
        ShowMessage(severity, title, message);
    }

    private void ShowMessage(InfoBarSeverity severity, string title, string message)
    {
        ValidationBar.Severity = severity;
        ValidationBar.Title = title;
        ValidationBar.Message = message;
        ValidationBar.IsOpen = true;
    }
}
