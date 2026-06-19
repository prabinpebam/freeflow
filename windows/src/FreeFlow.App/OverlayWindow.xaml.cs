using System;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;

namespace FreeFlow_App;

/// <summary>
/// Borderless, always-on-top recording indicator that mirrors the macOS
/// recording overlay. Shows a live audio meter while recording and a
/// "Transcribing…" state while the transcript is being produced. It is shown
/// <b>without activation</b> so it never steals foreground from the app the user
/// is dictating into (which would send the synthesized Ctrl+V to the overlay).
/// </summary>
public sealed partial class OverlayWindow : Window
{
    private const int BarCount = 13;
    private const int WidthDips = 300;
    private const int HeightDips = 78;

    private readonly Rectangle[] _bars = new Rectangle[BarCount];
    private readonly Random _rng = new();

    private static readonly Color RecordingColor = Color.FromArgb(0xFF, 0xFF, 0x45, 0x3A);
    private static readonly Color TranscribingColor = Color.FromArgb(0xFF, 0x0A, 0x84, 0xFF);

    public OverlayWindow()
    {
        InitializeComponent();
        ConfigureWindow();
        BuildBars();
    }

    private OverlappedPresenter? Presenter => AppWindow.Presenter as OverlappedPresenter;

    private void ConfigureWindow()
    {
        var presenter = AppWindow.Presenter as OverlappedPresenter ?? OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsAlwaysOnTop = true;
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        AppWindow.SetPresenter(presenter);
        AppWindow.IsShownInSwitchers = false;

        var hwnd = WindowNative.GetWindowHandle(this);
        var scale = GetScale(hwnd);
        var width = (int)(WidthDips * scale);
        var height = (int)(HeightDips * scale);

        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        var x = area.X + (area.Width - width) / 2;
        var y = area.Y + area.Height - height - (int)(48 * scale);

        AppWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }

    private void BuildBars()
    {
        var brush = new SolidColorBrush(RecordingColor);
        for (var i = 0; i < BarCount; i++)
        {
            var bar = new Rectangle
            {
                Width = 4,
                Height = 4,
                RadiusX = 2,
                RadiusY = 2,
                Fill = brush,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _bars[i] = bar;
            Bars.Children.Add(bar);
        }
    }

    /// <summary>Shows the overlay in its recording state without taking focus.</summary>
    public void ShowRecording()
    {
        StatusGlyph.Glyph = "\uE720"; // Microphone
        StatusGlyph.Foreground = new SolidColorBrush(RecordingColor);
        StatusText.Text = "Listening…";
        SetBarColor(RecordingColor);
        AppWindow.Show(activateWindow: false);
        Presenter?.SetBorderAndTitleBar(false, false);
    }

    /// <summary>Switches the overlay to the transcribing/processing state.</summary>
    public void ShowTranscribing()
    {
        StatusGlyph.Glyph = "\uE895"; // Sync
        StatusGlyph.Foreground = new SolidColorBrush(TranscribingColor);
        StatusText.Text = "Transcribing…";
        SetBarColor(TranscribingColor);
        for (var i = 0; i < _bars.Length; i++)
        {
            _bars[i].Height = 6;
        }
    }

    /// <summary>Hides the overlay.</summary>
    public void HideOverlay() => AppWindow.Hide();

    /// <summary>Updates the meter bars from a normalized 0..1 level.</summary>
    public void UpdateLevel(float level)
    {
        var clamped = Math.Clamp(level, 0f, 1f);
        var mid = _bars.Length / 2;
        for (var i = 0; i < _bars.Length; i++)
        {
            // Taper toward the edges and add a little jitter so the meter feels alive.
            var distance = Math.Abs(i - mid) / (double)mid;
            var taper = 1.0 - (distance * 0.55);
            var jitter = 0.85 + (_rng.NextDouble() * 0.3);
            var height = 4 + (clamped * 30 * taper * jitter);
            _bars[i].Height = Math.Max(4, height);
        }
    }

    private void SetBarColor(Color color)
    {
        var brush = new SolidColorBrush(color);
        foreach (var bar in _bars)
        {
            bar.Fill = brush;
        }
    }

    private static double GetScale(nint hwnd)
    {
        var dpi = GetDpiForWindow(hwnd);
        return dpi <= 0 ? 1.0 : dpi / 96.0;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);
}
