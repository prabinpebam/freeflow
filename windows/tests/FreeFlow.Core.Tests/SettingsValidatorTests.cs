using FluentAssertions;
using FreeFlow.Core.Providers;
using FreeFlow.Core.Settings;

namespace FreeFlow.Core.Tests;

[Trait("Tier", "L0")]
public class SettingsValidatorTests
{
    private static AppSettings Valid() => AppSettings.Defaults with
    {
        Providers = new ProviderConfiguration
        {
            Transcription = new ProviderSettings { Model = "whisper-large-v3", ApiKey = "sk-t" },
            PostProcessing = new ProviderSettings { Model = "llama-3.3-70b-versatile", ApiKey = "sk-p" },
        },
    };

    [Fact]
    public void Default_settings_are_valid_apart_from_missing_keys()
    {
        var result = SettingsValidator.Validate(AppSettings.Defaults);

        result.IsValid.Should().BeTrue();
        result.HasWarnings.Should().BeTrue();
        result.Issues.Should().Contain(i =>
            i.Field == "Providers.Transcription.ApiKey" && i.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public void Fully_configured_settings_have_no_warnings_or_errors()
    {
        var result = SettingsValidator.Validate(Valid());

        result.IsValid.Should().BeTrue();
        result.HasWarnings.Should().BeFalse();
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com")]
    [InlineData("")]
    public void Non_http_base_url_is_an_error(string baseUrl)
    {
        var settings = Valid() with
        {
            Providers = Valid().Providers with
            {
                Transcription = Valid().Providers.Transcription with { BaseUrl = baseUrl },
            },
        };

        var result = SettingsValidator.Validate(settings);

        result.HasErrors.Should().BeTrue();
        result.Issues.Should().Contain(i => i.Field == "Providers.Transcription.BaseUrl");
    }

    [Fact]
    public void Blank_model_is_an_error()
    {
        var settings = Valid() with
        {
            Providers = Valid().Providers with
            {
                Transcription = Valid().Providers.Transcription with { Model = "  " },
            },
        };

        SettingsValidator.Validate(settings).Issues
            .Should().Contain(i => i.Field == "Providers.Transcription.Model" && i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Non_positive_timeout_is_an_error()
    {
        var settings = Valid() with
        {
            Providers = Valid().Providers with
            {
                Transcription = Valid().Providers.Transcription with { Timeout = TimeSpan.Zero },
            },
        };

        SettingsValidator.Validate(settings).Issues
            .Should().Contain(i => i.Field == "Providers.Transcription.Timeout");
    }

    [Fact]
    public void History_cap_below_one_is_an_error()
    {
        var settings = Valid() with { General = new GeneralSettings { HistoryCap = 0 } };

        SettingsValidator.Validate(settings).Issues
            .Should().Contain(i => i.Field == "General.HistoryCap" && i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Post_processing_endpoint_is_ignored_when_cleanup_disabled()
    {
        var settings = Valid() with
        {
            Dictation = Valid().Dictation with { PostProcessingEnabled = false },
            Providers = Valid().Providers with
            {
                PostProcessing = new ProviderSettings { Model = "", ApiKey = "" },
            },
        };

        SettingsValidator.Validate(settings).Issues
            .Should().NotContain(i => i.Field.StartsWith("Providers.PostProcessing"));
    }
}
