using FluentAssertions;
using FreeFlow.Core.Context;
using FreeFlow.TestKit;

namespace FreeFlow.Core.Tests;

[Trait("Tier", "L1")]
public class PolicyAwareSelectionReaderTests
{
    [Fact]
    public async Task Prefers_uia_and_does_not_call_clipboard_when_uia_succeeds()
    {
        var uia = new FakeSelectionReader("from uia");
        var clip = new FakeSelectionReader("from clipboard");
        var reader = new PolicyAwareSelectionReader(
            new FakeForegroundAppProbe(new ForegroundAppInfo("notepad")), uia, clip);

        var result = await reader.TryReadSelectionAsync();

        result.Should().Be("from uia");
        uia.Reads.Should().Be(1);
        clip.Reads.Should().Be(0);
    }

    [Fact]
    public async Task Falls_back_to_clipboard_when_uia_returns_nothing()
    {
        var uia = new FakeSelectionReader(null);
        var clip = new FakeSelectionReader("from clipboard");
        var reader = new PolicyAwareSelectionReader(
            new FakeForegroundAppProbe(new ForegroundAppInfo("notepad")), uia, clip);

        var result = await reader.TryReadSelectionAsync();

        result.Should().Be("from clipboard");
        uia.Reads.Should().Be(1);
        clip.Reads.Should().Be(1);
    }

    [Fact]
    public async Task Terminal_app_never_invokes_clipboard_copy()
    {
        var uia = new FakeSelectionReader(null);
        var clip = new FakeSelectionReader("from clipboard");
        var reader = new PolicyAwareSelectionReader(
            new FakeForegroundAppProbe(new ForegroundAppInfo("pwsh.exe")), uia, clip);

        var result = await reader.TryReadSelectionAsync();

        result.Should().BeNull();
        uia.Reads.Should().Be(1);
        clip.Reads.Should().Be(0); // Ctrl+C must never be sent to a console
    }

    [Fact]
    public async Task Missing_uia_reader_still_uses_clipboard_for_normal_app()
    {
        var clip = new FakeSelectionReader("from clipboard");
        var reader = new PolicyAwareSelectionReader(
            new FakeForegroundAppProbe(new ForegroundAppInfo("notepad")), uiAutomation: null, clipboardCopy: clip);

        var result = await reader.TryReadSelectionAsync();

        result.Should().Be("from clipboard");
        clip.Reads.Should().Be(1);
    }

    [Fact]
    public async Task Unknown_foreground_app_is_treated_as_normal()
    {
        var uia = new FakeSelectionReader(null);
        var clip = new FakeSelectionReader("from clipboard");
        var reader = new PolicyAwareSelectionReader(
            new FakeForegroundAppProbe(null), uia, clip);

        var result = await reader.TryReadSelectionAsync();

        result.Should().Be("from clipboard");
        clip.Reads.Should().Be(1);
    }
}
