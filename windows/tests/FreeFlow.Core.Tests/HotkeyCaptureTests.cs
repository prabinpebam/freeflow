using FluentAssertions;
using FreeFlow.Core.Input;

namespace FreeFlow.Core.Tests;

[Trait("Tier", "L0")]
public class HotkeyCaptureTests
{
    [Fact]
    public void Capture_with_primary_key_builds_combination()
    {
        HotkeyCapture.TryCapture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x20, out var combo)
            .Should().BeTrue();
        combo.Format().Should().Be("Ctrl+Alt+Space");
    }

    [Fact]
    public void Capture_of_bare_control_promotes_to_right_ctrl()
    {
        HotkeyCapture.TryCapture(HotkeyModifiers.Control, 0, out var combo).Should().BeTrue();
        combo.VirtualKey.Should().Be(0xA3);
    }

    [Fact]
    public void Capture_of_nothing_fails()
    {
        HotkeyCapture.TryCapture(HotkeyModifiers.None, 0, out var combo).Should().BeFalse();
        combo.IsUnset.Should().BeTrue();
    }

    [Fact]
    public void Identical_bindings_conflict()
    {
        var combo = HotkeyCombination.Parse("Ctrl+Alt+V");
        HotkeyCapture.Conflicts(combo, combo).Should().BeTrue();
    }

    [Fact]
    public void Unset_never_conflicts()
    {
        HotkeyCapture.Conflicts(HotkeyCombination.None, HotkeyCombination.None).Should().BeFalse();
    }

    [Fact]
    public void Default_bindings_have_no_conflicts()
    {
        HotkeyCapture.FindConflicts(HotkeyBindings.Defaults).Should().BeEmpty();
    }

    [Fact]
    public void Duplicate_bindings_are_reported()
    {
        var bindings = HotkeyBindings.Defaults with
        {
            Toggle = HotkeyBindings.Defaults.PasteAgain,
        };

        HotkeyCapture.FindConflicts(bindings).Should().ContainSingle()
            .Which.Should().Contain("Ctrl+Alt+V");
    }
}
