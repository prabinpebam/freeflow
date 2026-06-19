using FluentAssertions;
using FreeFlow.Core.Corpus;

namespace FreeFlow.Core.Tests;

[Trait("Tier", "L0")]
public class CorpusLoaderTests
{
    private const string SampleCaseJson = """
    {
      "id": "case-1",
      "intent": "dictation",
      "metadata": {
        "app_name": "Code",
        "bundle_identifier": "com.microsoft.VSCode",
        "process_name": "Code.exe",
        "window_title": "porting-plan.md",
        "selected_text": ""
      },
      "pipeline": {
        "raw_transcript": "um hello world",
        "post_processed_transcript": "Hello world.",
        "context_summary": "Editing a markdown file.",
        "post_processing_status": "ok",
        "screenshot_status": "skipped",
        "audio_path": "./audio.wav",
        "screenshot_path": "./screenshot.jpg"
      },
      "settings": {
        "custom_vocabulary": "FreeFlow",
        "system_prompt": "Clean up the transcript.",
        "context_system_prompt": "Summarize the context."
      }
    }
    """;

    [Fact]
    public void Parses_case_json_into_typed_model()
    {
        var c = CorpusLoader.Parse(SampleCaseJson);

        c.Id.Should().Be("case-1");
        c.Intent.Should().Be("dictation");
        c.Metadata.AppName.Should().Be("Code");
        c.Metadata.ProcessName.Should().Be("Code.exe");
        c.Pipeline.RawTranscript.Should().Be("um hello world");
        c.Pipeline.PostProcessedTranscript.Should().Be("Hello world.");
        c.Pipeline.PostProcessingStatus.Should().Be("ok");
        c.Settings.CustomVocabulary.Should().Be("FreeFlow");
    }

    [Fact]
    public void Empty_json_throws()
    {
        var act = () => CorpusLoader.Parse("   ");

        act.Should().Throw<ArgumentException>();
    }
}
