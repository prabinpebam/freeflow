using System.Net;
using FluentAssertions;
using FreeFlow.Core.Providers;
using FreeFlow.Infrastructure.Providers;

namespace FreeFlow.Pipeline.Tests;

[Trait("Tier", "L2")]
public class ProviderConnectivityProbeTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        private readonly Exception? _throw;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public StubHandler(Exception toThrow) => _throw = toThrow;

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (_throw is not null)
            {
                throw _throw;
            }

            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body),
            });
        }
    }

    private static ProviderSettings Settings() => new()
    {
        BaseUrl = "https://api.example.com/v1",
        ApiKey = "secret-key",
        Model = "whisper-large-v3",
    };

    [Fact]
    public async Task Transcription_check_posts_audio_with_auth_and_succeeds()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"text\":\"\"}");
        var probe = new ProviderConnectivityProbe(new HttpClient(handler));

        var result = await probe.CheckTranscriptionAsync(Settings());

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should().Be("https://api.example.com/v1/audio/transcriptions");
        handler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.Should().Be("secret-key");
    }

    [Fact]
    public async Task Transcription_check_reports_auth_failure_with_hint()
    {
        var handler = new StubHandler(
            HttpStatusCode.Unauthorized, "{\"error\":{\"message\":\"Invalid API key\"}}");
        var probe = new ProviderConnectivityProbe(new HttpClient(handler));

        var result = await probe.CheckTranscriptionAsync(Settings());

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(401);
        result.Message.Should().Contain("Invalid API key");
        result.Message.Should().Contain("check the API key");
    }

    [Fact]
    public async Task PostProcessing_check_posts_chat_completions_and_succeeds()
    {
        var handler = new StubHandler(
            HttpStatusCode.OK, "{\"choices\":[{\"message\":{\"content\":\"OK\"}}]}");
        var probe = new ProviderConnectivityProbe(new HttpClient(handler));

        var result = await probe.CheckPostProcessingAsync(Settings() with { Model = "llama-3.3-70b-versatile" });

        result.Success.Should().BeTrue();
        handler.LastRequest!.RequestUri!.AbsoluteUri.Should().Be("https://api.example.com/v1/chat/completions");
        handler.LastRequest.Headers.Authorization!.Parameter.Should().Be("secret-key");
    }

    [Fact]
    public async Task Check_reports_network_error_without_throwing()
    {
        var handler = new StubHandler(new HttpRequestException("name resolution failed"));
        var probe = new ProviderConnectivityProbe(new HttpClient(handler));

        var result = await probe.CheckTranscriptionAsync(Settings());

        result.Success.Should().BeFalse();
        result.StatusCode.Should().BeNull();
        result.Message.Should().Contain("could not reach the endpoint");
    }
}
