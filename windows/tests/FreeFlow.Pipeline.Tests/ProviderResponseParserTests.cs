using FluentAssertions;
using FreeFlow.Infrastructure.Providers;

namespace FreeFlow.Pipeline.Tests;

[Trait("Tier", "L2")]
public class ProviderResponseParserTests
{
    [Fact]
    public void Parses_canonical_transcription_text()
    {
        ProviderResponseParser.ParseTranscription("{\"text\":\"  hello world  \"}")
            .Should().Be("hello world");
    }

    [Fact]
    public void Parses_verbose_transcription_segments()
    {
        var json = "{\"segments\":[{\"text\":\"hello \"},{\"text\":\"world\"}]}";
        ProviderResponseParser.ParseTranscription(json).Should().Be("hello world");
    }

    [Fact]
    public void Empty_transcription_body_returns_empty()
    {
        ProviderResponseParser.ParseTranscription("").Should().BeEmpty();
    }

    [Fact]
    public void Transcription_without_text_throws()
    {
        var act = () => ProviderResponseParser.ParseTranscription("{\"foo\":1}");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parses_chat_completion_content()
    {
        var json = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"Hello world.\"}}]}";
        ProviderResponseParser.ParseChatCompletion(json).Should().Be("Hello world.");
    }

    [Fact]
    public void Chat_completion_without_choices_throws()
    {
        var act = () => ProviderResponseParser.ParseChatCompletion("{\"choices\":[]}");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parses_structured_error_message()
    {
        var json = "{\"error\":{\"message\":\"Invalid API key\",\"type\":\"auth\"}}";
        ProviderResponseParser.ParseErrorMessage(json).Should().Be("Invalid API key");
    }

    [Fact]
    public void Non_json_error_body_is_returned_as_is()
    {
        ProviderResponseParser.ParseErrorMessage("Service Unavailable")
            .Should().Be("Service Unavailable");
    }
}
