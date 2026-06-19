using FluentAssertions;
using FreeFlow.Core.Input;

namespace FreeFlow.Core.Tests;

[Trait("Tier", "L0")]
public class HotkeyCombinationTests
{
    [Theory]
    [InlineData("Ctrl+Alt+Space")]
    [InlineData("Ctrl+Alt+V")]
    [InlineData("Shift+F5")]
    [InlineData("Win+Up")]
    public void Parse_then_format_round_trips_to_canonical_string(string canonical)
    {
        var combo = HotkeyCombination.Parse(canonical);
        combo.Format().Should().Be(canonical);
    }

    [Fact]
    public void Parse_is_case_and_order_insensitive()
    {
        var a = HotkeyCombination.Parse("ctrl+alt+space");
        var b = HotkeyCombination.Parse("ALT+SPACE+CTRL");
        a.Should().Be(b);
        a.Format().Should().Be("Ctrl+Alt+Space");
    }

    [Fact]
    public void Modifier_aliases_map_to_same_combination()
    {
        HotkeyCombination.Parse("Control+Space")
            .Should().Be(HotkeyCombination.Parse("Ctrl+Space"));
        HotkeyCombination.Parse("Windows+A")
            .Should().Be(HotkeyCombination.Parse("Win+A"));
    }

    [Fact]
    public void Modifier_only_shortcut_encodes_modifier_as_primary_key()
    {
        var rightCtrl = HotkeyCombination.Parse("RightCtrl");
        rightCtrl.IsUnset.Should().BeFalse();
        rightCtrl.IsModifierOnly.Should().BeTrue();
        rightCtrl.Format().Should().Be("RightCtrl");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Ctrl+Boguskey")]
    [InlineData("Ctrl+A+B")]
    public void Parse_rejects_invalid_input(string text)
    {
        var act = () => HotkeyCombination.Parse(text);
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void TryParse_reports_failure_without_throwing()
    {
        HotkeyCombination.TryParse("not a key", out var combo).Should().BeFalse();
        combo.Should().Be(HotkeyCombination.None);

        HotkeyCombination.TryParse("Ctrl+Alt+Space", out var ok).Should().BeTrue();
        ok.Should().Be(HotkeyCombination.Parse("Ctrl+Alt+Space"));
    }

    [Fact]
    public void Default_bindings_match_adr_009()
    {
        var d = HotkeyBindings.Defaults;
        d.HoldToTalk.Format().Should().Be("RightCtrl");
        d.Toggle.Format().Should().Be("Ctrl+Alt+Space");
        d.PasteAgain.Format().Should().Be("Ctrl+Alt+V");
    }

    [Fact]
    public void Letters_and_digits_parse_to_virtual_key_codes()
    {
        HotkeyCombination.Parse("Ctrl+A").VirtualKey.Should().Be('A');
        HotkeyCombination.Parse("Ctrl+7").VirtualKey.Should().Be('7');
    }
}
