using FluentAssertions;
using FreeFlow.Core.Context;
using FreeFlow.Core.Pipeline;
using FreeFlow.Core.Settings;

namespace FreeFlow.Core.Tests;

[Trait("Tier", "L1")]
public class ContextPromptBuilderTests
{
    private static PostProcessingRequest Request(
        DictationIntent intent = DictationIntent.Dictation,
        CaptureContext? context = null,
        DictationSettings? settings = null)
        => new("raw words", settings ?? new DictationSettings(), context ?? CaptureContext.None, intent);

    [Fact]
    public void Dictation_uses_the_default_cleanup_prompt()
    {
        var prompt = ContextPromptBuilder.BuildSystemPrompt(Request());
        prompt.Should().StartWith(ContextPromptBuilder.DefaultDictationPrompt);
    }

    [Fact]
    public void Dictation_includes_app_and_window_context()
    {
        var context = new CaptureContext("Notepad", "notepad", "Untitled - Notepad", null);
        var prompt = ContextPromptBuilder.BuildSystemPrompt(Request(context: context));

        prompt.Should().Contain("dictating into Notepad");
        prompt.Should().Contain("(Untitled - Notepad)");
    }

    [Fact]
    public void Custom_system_prompt_overrides_the_default()
    {
        var settings = new DictationSettings { SystemPrompt = "Be terse." };
        var prompt = ContextPromptBuilder.BuildSystemPrompt(Request(settings: settings));

        prompt.Should().StartWith("Be terse.");
        prompt.Should().NotContain(ContextPromptBuilder.DefaultDictationPrompt);
    }

    [Fact]
    public void Vocabulary_and_language_clauses_are_appended()
    {
        var settings = new DictationSettings { CustomVocabulary = "Kubernetes", OutputLanguage = "French" };
        var prompt = ContextPromptBuilder.BuildSystemPrompt(Request(settings: settings));

        prompt.Should().Contain("Preserve these terms exactly: Kubernetes.");
        prompt.Should().Contain("Translate the output into French.");
    }

    [Fact]
    public void Command_mode_with_selection_embeds_the_selected_text()
    {
        var context = new CaptureContext("Word", "winword", "Doc - Word", "the quick brown fox");
        var prompt = ContextPromptBuilder.BuildSystemPrompt(
            Request(DictationIntent.CommandManual, context));

        prompt.Should().StartWith(ContextPromptBuilder.DefaultCommandPrompt);
        prompt.Should().Contain("Selected text:");
        prompt.Should().Contain("the quick brown fox");
    }

    [Fact]
    public void Command_mode_without_selection_falls_back()
    {
        var prompt = ContextPromptBuilder.BuildSystemPrompt(
            Request(DictationIntent.CommandAutomatic));

        prompt.Should().StartWith(ContextPromptBuilder.DefaultCommandPrompt);
        prompt.Should().Contain("No text is selected");
        prompt.Should().NotContain("Selected text:");
    }

    [Fact]
    public void Command_mode_uses_context_system_prompt_override()
    {
        var settings = new DictationSettings { ContextSystemPrompt = "Edit precisely." };
        var context = new CaptureContext(null, null, null, "hello");
        var prompt = ContextPromptBuilder.BuildSystemPrompt(
            Request(DictationIntent.CommandManual, context, settings));

        prompt.Should().StartWith("Edit precisely.");
    }
}
