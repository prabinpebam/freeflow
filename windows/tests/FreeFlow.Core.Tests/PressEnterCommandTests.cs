using FluentAssertions;
using FreeFlow.Core.Pipeline;

namespace FreeFlow.Core.Tests;

[Trait("Tier", "L0")]
public class PressEnterCommandTests
{
    [Theory]
    [InlineData("Send the message, press enter", "Send the message")]
    [InlineData("Send the message press enter.", "Send the message")]
    [InlineData("hello world press   enter", "hello world")]
    [InlineData("press enter", "")]
    public void Detects_and_strips_trailing_press_enter(string input, string expected)
    {
        var (text, pressEnter) = PressEnterCommand.Detect(input);

        pressEnter.Should().BeTrue();
        text.Should().Be(expected);
    }

    [Theory]
    [InlineData("Press enter to continue")]
    [InlineData("I will press enter key myself")]
    [InlineData("just some dictation")]
    [InlineData("")]
    public void Leaves_text_without_trailing_command_unchanged(string input)
    {
        var (text, pressEnter) = PressEnterCommand.Detect(input);

        pressEnter.Should().BeFalse();
        text.Should().Be(input);
    }

    [Fact]
    public void Null_input_is_safe()
    {
        var (text, pressEnter) = PressEnterCommand.Detect(null);

        pressEnter.Should().BeFalse();
        text.Should().Be(string.Empty);
    }
}
