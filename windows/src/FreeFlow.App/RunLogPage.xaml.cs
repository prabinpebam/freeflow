using System;
using System.Collections.Generic;
using System.Linq;
using FreeFlow.Core.History;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FreeFlow_App;

/// <summary>
/// Read-only viewer over the persisted run history (<see cref="IHistoryStore"/>),
/// the Windows analog of the macOS run-log window. Entries are loaded newest-first
/// and projected to a flat, binding-friendly row.
/// </summary>
public sealed partial class RunLogPage : Page
{
    private readonly IHistoryStore _history;

    public RunLogPage()
    {
        InitializeComponent();
        _history = App.Services.GetRequiredService<IHistoryStore>();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadAsync();
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        IReadOnlyList<HistoryEntry> entries;
        try
        {
            entries = await _history.LoadAsync();
        }
        catch
        {
            entries = Array.Empty<HistoryEntry>();
        }

        var rows = entries.Select(HistoryRow.From).ToList();
        HistoryList.ItemsSource = rows;
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SummaryText.Text = rows.Count switch
        {
            0 => "No runs recorded yet.",
            1 => "1 run recorded.",
            _ => $"{rows.Count} runs recorded (newest first).",
        };
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
        {
            Frame.GoBack();
        }
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
        => await LoadAsync();

    private async void OnClearClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Clear run history?",
            Content = "This permanently removes all recorded runs. This cannot be undone.",
            PrimaryButtonText = "Clear",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await _history.ClearAsync();
            await LoadAsync();
        }
    }

    private sealed class HistoryRow
    {
        public string Header { get; init; } = string.Empty;
        public string Cleaned { get; init; } = string.Empty;
        public string Raw { get; init; } = string.Empty;

        public static HistoryRow From(HistoryEntry entry)
        {
            var app = entry.AppName ?? "unknown app";
            var title = string.IsNullOrWhiteSpace(entry.WindowTitle) ? "" : $" — {entry.WindowTitle}";
            var pasted = entry.Pasted ? "pasted" : "not pasted";
            var status = string.IsNullOrWhiteSpace(entry.PostProcessingStatus)
                ? pasted
                : $"{entry.PostProcessingStatus}, {pasted}";

            return new HistoryRow
            {
                Header = $"{entry.Timestamp.LocalDateTime:g} · {entry.Intent} · {app}{title} · {status}",
                Cleaned = string.IsNullOrEmpty(entry.PostProcessedTranscript)
                    ? "(no cleaned text)"
                    : entry.PostProcessedTranscript,
                Raw = string.IsNullOrEmpty(entry.RawTranscript)
                    ? string.Empty
                    : $"Raw: {entry.RawTranscript}",
            };
        }
    }
}
