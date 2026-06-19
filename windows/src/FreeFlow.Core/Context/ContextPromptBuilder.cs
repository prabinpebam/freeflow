using System.Text;
using FreeFlow.Core.Context;
using FreeFlow.Core.Pipeline;

namespace FreeFlow.Core.Context;

/// <summary>
/// Builds the chat-completions system prompt for the post-processing step from a
/// <see cref="PostProcessingRequest"/>. Pure and deterministic so prompt shape is
/// asserted directly in the inner loop (rather than only through the HTTP client).
/// Two modes are produced:
/// <list type="bullet">
/// <item>Dictation — clean up the spoken transcript (the user message).</item>
/// <item>Command/edit — apply the spoken instruction (the user message) to the
/// captured selected text, which is embedded in the system prompt.</item>
/// </list>
/// </summary>
public static class ContextPromptBuilder
{
    public const string DefaultDictationPrompt =
        "You clean up dictated text. Remove filler words and fix punctuation and capitalization. "
        + "Preserve the speaker's meaning and wording. Never invent names or facts. "
        + "Return only the cleaned text with no preamble.";

    public const string DefaultCommandPrompt =
        "You edit text based on the user's spoken instruction. Apply the instruction to the selected "
        + "text below and return only the edited text with no preamble or explanation.";

    public static string BuildSystemPrompt(PostProcessingRequest request)
    {
        var sb = new StringBuilder();

        if (IsCommand(request.Intent))
        {
            BuildCommandPrompt(sb, request);
        }
        else
        {
            BuildDictationPrompt(sb, request);
        }

        return sb.ToString();
    }

    private static void BuildDictationPrompt(StringBuilder sb, PostProcessingRequest request)
    {
        sb.Append(string.IsNullOrWhiteSpace(request.Settings.SystemPrompt)
            ? DefaultDictationPrompt
            : request.Settings.SystemPrompt);

        AppendVocabulary(sb, request);
        AppendLanguage(sb, request);
        AppendContext(sb, request.Context);
    }

    private static void BuildCommandPrompt(StringBuilder sb, PostProcessingRequest request)
    {
        sb.Append(string.IsNullOrWhiteSpace(request.Settings.ContextSystemPrompt)
            ? DefaultCommandPrompt
            : request.Settings.ContextSystemPrompt);

        AppendContext(sb, request.Context);

        if (request.Context.HasSelection)
        {
            sb.Append("\nSelected text:\n\"\"\"\n")
              .Append(request.Context.SelectedText)
              .Append("\n\"\"\"");
        }
        else
        {
            // Baseline fallback: no selection was captured, so there is nothing to
            // transform — instruct the model to echo the instruction verbatim.
            sb.Append("\nNo text is selected; return the instruction unchanged.");
        }

        AppendVocabulary(sb, request);
        AppendLanguage(sb, request);
    }

    private static void AppendVocabulary(StringBuilder sb, PostProcessingRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Settings.CustomVocabulary))
        {
            sb.Append("\nPreserve these terms exactly: ").Append(request.Settings.CustomVocabulary).Append('.');
        }
    }

    private static void AppendLanguage(StringBuilder sb, PostProcessingRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Settings.OutputLanguage))
        {
            sb.Append("\nTranslate the output into ").Append(request.Settings.OutputLanguage).Append('.');
        }
    }

    private static void AppendContext(StringBuilder sb, CaptureContext context)
    {
        if (context is null || context == CaptureContext.None)
        {
            return;
        }

        var app = context.AppName ?? context.ProcessName;
        if (!string.IsNullOrWhiteSpace(app))
        {
            sb.Append("\nThe user is dictating into ").Append(app);
            if (!string.IsNullOrWhiteSpace(context.WindowTitle))
            {
                sb.Append(" (").Append(context.WindowTitle).Append(')');
            }

            sb.Append('.');
        }
    }

    private static bool IsCommand(DictationIntent intent)
        => intent is DictationIntent.CommandAutomatic or DictationIntent.CommandManual;
}
