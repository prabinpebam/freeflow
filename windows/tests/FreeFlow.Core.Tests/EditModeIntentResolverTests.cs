using FluentAssertions;
using FreeFlow.Core.Pipeline;
using FreeFlow.Core.Settings;

namespace FreeFlow.Core.Tests;

[Trait("Tier", "L0")]
public class EditModeIntentResolverTests
{
    [Fact]
    public void Disabled_always_resolves_to_dictation()
    {
        var settings = new EditModeSettings { Enabled = false, Style = CommandModeStyle.Automatic };

        EditModeIntentResolver.Resolve(settings, hasSelection: true, manualModifierHeld: true)
            .Should().Be(DictationIntent.Dictation);
    }

    [Fact]
    public void Automatic_with_selection_is_command_automatic()
    {
        var settings = new EditModeSettings { Enabled = true, Style = CommandModeStyle.Automatic };

        EditModeIntentResolver.Resolve(settings, hasSelection: true, manualModifierHeld: false)
            .Should().Be(DictationIntent.CommandAutomatic);
    }

    [Fact]
    public void Automatic_without_selection_falls_back_to_dictation()
    {
        var settings = new EditModeSettings { Enabled = true, Style = CommandModeStyle.Automatic };

        EditModeIntentResolver.Resolve(settings, hasSelection: false, manualModifierHeld: true)
            .Should().Be(DictationIntent.Dictation);
    }

    [Fact]
    public void Manual_with_modifier_and_selection_is_command_manual()
    {
        var settings = new EditModeSettings { Enabled = true, Style = CommandModeStyle.Manual };

        EditModeIntentResolver.Resolve(settings, hasSelection: true, manualModifierHeld: true)
            .Should().Be(DictationIntent.CommandManual);
    }

    [Theory]
    [InlineData(true, false)]  // modifier held but nothing selected
    [InlineData(false, true)]  // selection but modifier not held
    [InlineData(false, false)]
    public void Manual_falls_back_to_dictation_unless_modifier_and_selection(bool hasSelection, bool modifierHeld)
    {
        var settings = new EditModeSettings { Enabled = true, Style = CommandModeStyle.Manual };

        EditModeIntentResolver.Resolve(settings, hasSelection, modifierHeld)
            .Should().Be(DictationIntent.Dictation);
    }
}
