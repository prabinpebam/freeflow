using FluentAssertions;
using FreeFlow.Core.Providers;
using FreeFlow.Infrastructure.Providers;

namespace FreeFlow.Pipeline.Tests;

[Trait("Tier", "L0")]
public class ProviderEndpointsTests
{
    [Fact]
    public void OpenAiCompatible_appends_operation_to_base_url()
    {
        var settings = new ProviderSettings
        {
            BaseUrl = "https://api.groq.com/openai/v1",
            Model = "whisper-large-v3",
        };

        var uri = ProviderEndpoints.Resolve(settings, ProviderEndpoints.Transcriptions);

        uri.AbsoluteUri.Should().Be("https://api.groq.com/openai/v1/audio/transcriptions");
    }

    [Fact]
    public void OpenAiCompatible_trailing_slash_in_base_url_is_normalised()
    {
        var settings = new ProviderSettings
        {
            BaseUrl = "https://api.groq.com/openai/v1/",
            Model = "llama-3.3-70b-versatile",
        };

        var uri = ProviderEndpoints.Resolve(settings, ProviderEndpoints.ChatCompletions);

        uri.AbsoluteUri.Should().Be("https://api.groq.com/openai/v1/chat/completions");
    }

    [Fact]
    public void Azure_builds_deployment_url_with_api_version()
    {
        var settings = new ProviderSettings
        {
            BaseUrl = "https://ai-project-deployments-resource.cognitiveservices.azure.com",
            Model = "gpt-4o-transcribe",
            ApiVersion = "2025-03-01-preview",
        };

        var uri = ProviderEndpoints.Resolve(settings, ProviderEndpoints.Transcriptions);

        uri.AbsoluteUri.Should().Be(
            "https://ai-project-deployments-resource.cognitiveservices.azure.com/openai/deployments/gpt-4o-transcribe/audio/transcriptions?api-version=2025-03-01-preview");
    }

    [Fact]
    public void Azure_chat_completions_uses_deployment_path()
    {
        var settings = new ProviderSettings
        {
            BaseUrl = "https://res.cognitiveservices.azure.com/",
            Model = "gpt-4o",
            ApiVersion = "2025-03-01-preview",
        };

        var uri = ProviderEndpoints.Resolve(settings, ProviderEndpoints.ChatCompletions);

        uri.AbsoluteUri.Should().Be(
            "https://res.cognitiveservices.azure.com/openai/deployments/gpt-4o/chat/completions?api-version=2025-03-01-preview");
    }

    [Fact]
    public void Azure_without_deployment_model_throws()
    {
        var settings = new ProviderSettings
        {
            BaseUrl = "https://res.cognitiveservices.azure.com",
            Model = "",
            ApiVersion = "2025-03-01-preview",
        };

        var act = () => ProviderEndpoints.Resolve(settings, ProviderEndpoints.Transcriptions);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsAzure_reflects_api_version_presence()
    {
        new ProviderSettings().IsAzure.Should().BeFalse();
        new ProviderSettings { ApiVersion = "2025-03-01-preview" }.IsAzure.Should().BeTrue();
    }
}
