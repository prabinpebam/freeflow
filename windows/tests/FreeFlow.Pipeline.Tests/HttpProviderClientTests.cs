using System.Net;
using FluentAssertions;
using FreeFlow.Core.Audio;
using FreeFlow.Core.Context;
using FreeFlow.Core.Pipeline;
using FreeFlow.Core.Providers;
using FreeFlow.Core.Settings;
using FreeFlow.Infrastructure.Providers;

namespace FreeFlow.Pipeline.Tests;

[Trait("Tier", "L2")]
public class HttpProviderClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (request.Content is not null)
            {
                LastRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body),
            };
        }
    }

    private static ProviderSettings Settings(string model) => new()
    {
        BaseUrl = "https://api.example.com/v1",
        ApiKey = "secret-key",
        Model = model,
    };

    [Fact]
    public async Task Transcription_posts_to_endpoint_with_auth_and_parses_text()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"text\":\"hello world\"}");
        var client = new HttpTranscriptionClient(new HttpClient(handler), Settings("whisper-large-v3"));

        var result = await client.TranscribeAsync(
            new AudioClip(new byte[3200]),
            new TranscriptionOptions("FreeFlow"));

        result.Should().Be("hello world");
        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.AbsoluteUri.Should().Be("https://api.example.com/v1/audio/transcriptions");
        handler.LastRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.Should().Be("secret-key");
    }

    [Fact]
    public async Task Transcription_empty_clip_skips_network()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"text\":\"unused\"}");
        var client = new HttpTranscriptionClient(new HttpClient(handler), Settings("whisper-large-v3"));

        var result = await client.TranscribeAsync(AudioClip.Empty, new TranscriptionOptions(""));

        result.Should().BeEmpty();
        handler.LastRequest.Should().BeNull();
    }

    [Fact]
    public async Task Transcription_error_status_throws_provider_exception()
    {
        var handler = new StubHandler(HttpStatusCode.Unauthorized, "{\"error\":{\"message\":\"Invalid API key\"}}");
        var client = new HttpTranscriptionClient(new HttpClient(handler), Settings("whisper-large-v3"));

        var act = () => client.TranscribeAsync(new AudioClip(new byte[3200]), new TranscriptionOptions(""));

        var ex = await act.Should().ThrowAsync<ProviderException>();
        ex.Which.StatusCode.Should().Be(401);
        ex.Which.Message.Should().Be("Invalid API key");
    }

    [Fact]
    public async Task Cleanup_posts_chat_completions_and_parses_content()
    {
        var handler = new StubHandler(
            HttpStatusCode.OK,
            "{\"choices\":[{\"message\":{\"content\":\"Hello world.\"}}]}");
        var client = new HttpPostProcessingClient(new HttpClient(handler), Settings("llama-3.3-70b-versatile"));

        var result = await client.CleanupAsync(new PostProcessingRequest(
            "um hello world",
            new DictationSettings(),
            CaptureContext.None));

        result.Should().Be("Hello world.");
        handler.LastRequest!.RequestUri!.AbsoluteUri.Should().Be("https://api.example.com/v1/chat/completions");
        handler.LastRequestBody.Should().Contain("\"temperature\":0");
        handler.LastRequestBody.Should().Contain("um hello world");
    }

    [Fact]
    public async Task Cleanup_never_blanks_non_empty_transcript()
    {
        var handler = new StubHandler(
            HttpStatusCode.OK,
            "{\"choices\":[{\"message\":{\"content\":\"   \"}}]}");
        var client = new HttpPostProcessingClient(new HttpClient(handler), Settings("llama-3.3-70b-versatile"));

        var result = await client.CleanupAsync(new PostProcessingRequest(
            "keep these words",
            new DictationSettings(),
            CaptureContext.None));

        result.Should().Be("keep these words");
    }

    [Fact]
    public async Task Cleanup_empty_input_skips_network()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "unused");
        var client = new HttpPostProcessingClient(new HttpClient(handler), Settings("llama-3.3-70b-versatile"));

        var result = await client.CleanupAsync(new PostProcessingRequest(
            "   ",
            new DictationSettings(),
            CaptureContext.None));

        result.Should().BeEmpty();
        handler.LastRequest.Should().BeNull();
    }
}
