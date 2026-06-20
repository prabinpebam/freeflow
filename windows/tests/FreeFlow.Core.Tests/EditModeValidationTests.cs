using FluentAssertions;
using FreeFlow.Core.Settings;

namespace FreeFlow.Core.Tests;

[Trait("Tier", "L0")]
public class EditModeValidationTests
{
    private static AppSettings WithEditMode(EditModeSettings editMode) =>
        AppSettings.Defaults with { Dictation = new DictationSettings { EditMode = editMode } };

    [Fact]
    public void Manual_modifier_that_collides_with_default_shortcuts_is_an_error()
    {
        // Defaults: hold = RightCtrl, toggle/paste-again include Ctrl and Alt.
        var settings = WithEditMode(new EditModeSettings
        {
            Enabled = true,
            Style = CommandModeStyle.Manual,
            ManualModifier = CommandModeModifier.Control,
        });

        var result = SettingsValidator.Validate(settings);

        result.Issues.Should().Contain(i =>
            i.Field == "Dictation.EditMode.ManualModifier" && i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Manual_modifier_that_is_distinct_is_accepted()
    {
        // Shift is not part of any default shortcut.
        var settings = WithEditMode(new EditModeSettings
        {
            Enabled = true,
            Style = CommandModeStyle.Manual,
            ManualModifier = CommandModeModifier.Shift,
        });

        var result = SettingsValidator.Validate(settings);

        result.Issues.Should().NotContain(i => i.Field == "Dictation.EditMode.ManualModifier");
    }

    [Theory]
    [InlineData(false, CommandModeStyle.Manual)]   // disabled
    [InlineData(true, CommandModeStyle.Automatic)] // automatic doesn't use a modifier
    public void No_modifier_check_when_disabled_or_automatic(bool enabled, CommandModeStyle style)
    {
        var settings = WithEditMode(new EditModeSettings
        {
            Enabled = enabled,
            Style = style,
            ManualModifier = CommandModeModifier.Control,
        });

        var result = SettingsValidator.Validate(settings);

        result.Issues.Should().NotContain(i => i.Field == "Dictation.EditMode.ManualModifier");
    }
}
