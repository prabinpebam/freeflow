using FluentAssertions;
using FreeFlow.Core.Context;

namespace FreeFlow.Core.Tests;

[Trait("Tier", "L0")]
public class AppCompatibilityPolicyTests
{
    [Theory]
    [InlineData("notepad")]
    [InlineData("Word")]
    [InlineData("chrome.exe")]
    [InlineData("Code")]
    public void Normal_apps_try_uia_then_clipboard(string process)
    {
        var order = AppCompatibilityPolicy.StrategiesFor(new ForegroundAppInfo(process));
        order.Should().Equal(SelectionStrategy.UiAutomation, SelectionStrategy.ClipboardCopy);
    }

    [Theory]
    [InlineData("cmd")]
    [InlineData("powershell")]
    [InlineData("pwsh.exe")]
    [InlineData("WindowsTerminal")]
    [InlineData("wt")]
    [InlineData("conhost")]
    public void Terminals_never_fall_back_to_clipboard_copy(string process)
    {
        var app = new ForegroundAppInfo(process);
        AppCompatibilityPolicy.IsClipboardCopyUnsafe(app).Should().BeTrue();
        AppCompatibilityPolicy.StrategiesFor(app).Should().Equal(SelectionStrategy.UiAutomation);
    }

    [Fact]
    public void Unknown_app_is_treated_as_normal()
    {
        AppCompatibilityPolicy.IsClipboardCopyUnsafe(null).Should().BeFalse();
        AppCompatibilityPolicy.StrategiesFor(null)
            .Should().Equal(SelectionStrategy.UiAutomation, SelectionStrategy.ClipboardCopy);
    }

    [Fact]
    public void Process_name_matching_is_case_and_extension_insensitive()
    {
        AppCompatibilityPolicy.IsClipboardCopyUnsafe(new ForegroundAppInfo("PWSH.EXE")).Should().BeTrue();
        AppCompatibilityPolicy.IsClipboardCopyUnsafe(new ForegroundAppInfo(" cmd ")).Should().BeTrue();
    }
}
