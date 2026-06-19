using System.Text.Json;
using System.Text.Json.Serialization;

namespace FreeFlow.Core.Corpus;

/// <summary>
/// Strongly-typed view of a golden-corpus <c>case.json</c> exported by the
/// macOS app. Used as the cross-platform oracle for pipeline inputs/outputs.
/// </summary>
public sealed class PipelineCase
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("intent")] public string Intent { get; set; } = "dictation";
    [JsonPropertyName("metadata")] public PipelineCaseMetadata Metadata { get; set; } = new();
    [JsonPropertyName("pipeline")] public PipelineCasePipeline Pipeline { get; set; } = new();
    [JsonPropertyName("settings")] public PipelineCaseSettings Settings { get; set; } = new();
}

public sealed class PipelineCaseMetadata
{
    [JsonPropertyName("app_name")] public string? AppName { get; set; }
    [JsonPropertyName("bundle_identifier")] public string? BundleIdentifier { get; set; }
    [JsonPropertyName("process_name")] public string? ProcessName { get; set; }
    [JsonPropertyName("window_title")] public string? WindowTitle { get; set; }
    [JsonPropertyName("selected_text")] public string? SelectedText { get; set; }
}

public sealed class PipelineCasePipeline
{
    [JsonPropertyName("raw_transcript")] public string RawTranscript { get; set; } = string.Empty;
    [JsonPropertyName("post_processed_transcript")] public string PostProcessedTranscript { get; set; } = string.Empty;
    [JsonPropertyName("context_summary")] public string ContextSummary { get; set; } = string.Empty;
    [JsonPropertyName("post_processing_status")] public string PostProcessingStatus { get; set; } = string.Empty;
    [JsonPropertyName("screenshot_status")] public string ScreenshotStatus { get; set; } = string.Empty;
    [JsonPropertyName("audio_path")] public string? AudioPath { get; set; }
    [JsonPropertyName("screenshot_path")] public string? ScreenshotPath { get; set; }
}

public sealed class PipelineCaseSettings
{
    [JsonPropertyName("custom_vocabulary")] public string CustomVocabulary { get; set; } = string.Empty;
    [JsonPropertyName("system_prompt")] public string SystemPrompt { get; set; } = string.Empty;
    [JsonPropertyName("context_system_prompt")] public string ContextSystemPrompt { get; set; } = string.Empty;
}

public static class CorpusLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static PipelineCase Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("case.json content is empty.", nameof(json));
        }

        return JsonSerializer.Deserialize<PipelineCase>(json, Options)
               ?? throw new InvalidOperationException("Failed to deserialize case.json.");
    }

    public static PipelineCase LoadFromFile(string caseJsonPath)
        => Parse(File.ReadAllText(caseJsonPath));
}
