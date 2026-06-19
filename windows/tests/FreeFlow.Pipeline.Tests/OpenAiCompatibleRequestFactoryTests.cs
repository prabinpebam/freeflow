using FluentAssertions;
using FreeFlow.Infrastructure.Providers;

namespace FreeFlow.Pipeline.Tests;

[Trait("Tier", "L2")]
public class OpenAiCompatibleRequestFactoryTests
{
    [Fact]
    public void Transcription_fields_include_model_format_and_optional_language()
    {
        var fields = OpenAiCompatibleRequestFactory.BuildTranscriptionFields(
            "whisper-large-v3", language: "en");

        fields.Should().Contain(new KeyValuePair<string, string>("model", "whisper-large-v3"));
        fields.Should().Contain(new KeyValuePair<string, string>("response_format", "json"));
        fields.Should().Contain(new KeyValuePair<string, string>("language", "en"));
    }

    [Fact]
    public void Transcription_fields_omit_language_when_not_provided()
    {
        var fields = OpenAiCompatibleRequestFactory.BuildTranscriptionFields("whisper-large-v3");

        fields.Should().NotContain(f => f.Key == "language");
    }

    [Fact]
    public void Chat_body_is_deterministic_and_zero_temperature()
    {
        var body = OpenAiCompatibleRequestFactory.BuildChatCompletionsBody(
            "llama-3.3-70b-versatile", "sys", "user text");

        body.Should().Be(
            "{\"model\":\"llama-3.3-70b-versatile\",\"messages\":[" +
            "{\"role\":\"system\",\"content\":\"sys\"}," +
            "{\"role\":\"user\",\"content\":\"user text\"}]," +
            "\"temperature\":0}");
    }
}
