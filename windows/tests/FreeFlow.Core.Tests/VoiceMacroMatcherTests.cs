using FluentAssertions;
using FreeFlow.Core.Macros;

namespace FreeFlow.Core.Tests;

[Trait("Tier", "L0")]
public class VoiceMacroMatcherTests
{
    private static readonly VoiceMacro[] Macros =
    {
        new() { Command = "Insert my email", Payload = "me@example.com" },
        new() { Command = "Sign off", Payload = "Best regards,\nPat" },
    };

    [Theory]
    [InlineData("insert my email")]
    [InlineData("Insert my email.")]
    [InlineData("  INSERT MY EMAIL  ")]
    [InlineData("Insert, my email!")]
    public void Matches_loosely_ignoring_case_punctuation_and_whitespace(string transcript)
    {
        var match = VoiceMacroMatcher.Match(transcript, Macros);

        match.Should().NotBeNull();
        match!.Payload.Should().Be("me@example.com");
    }

    [Theory]
    [InlineData("insert my email address")]
    [InlineData("please insert my email")]
    [InlineData("email")]
    public void Does_not_match_when_phrase_is_not_the_whole_transcript(string transcript)
        => VoiceMacroMatcher.Match(transcript, Macros).Should().BeNull();

    [Fact]
    public void Returns_null_for_empty_transcript_or_no_macros()
    {
        VoiceMacroMatcher.Match("", Macros).Should().BeNull();
        VoiceMacroMatcher.Match("insert my email", null).Should().BeNull();
        VoiceMacroMatcher.Match("insert my email", System.Array.Empty<VoiceMacro>()).Should().BeNull();
    }

    [Fact]
    public void First_matching_macro_wins()
    {
        var macros = new[]
        {
            new VoiceMacro { Command = "go", Payload = "first" },
            new VoiceMacro { Command = "go", Payload = "second" },
        };

        VoiceMacroMatcher.Match("go", macros)!.Payload.Should().Be("first");
    }
}
