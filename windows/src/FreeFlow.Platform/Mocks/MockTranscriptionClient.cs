using FreeFlow.Core.Abstractions;
using FreeFlow.Core.Audio;
using FreeFlow.Core.Pipeline;

namespace FreeFlow.Platform.Mocks;

/// <summary>
/// Phase-2 placeholder for the real speech-to-text client. Returns a canned
/// transcript so the UI shows a realistic round-trip. Replaced by a real
/// OpenAI-compatible client in Phase 3.
/// </summary>
public sealed class MockTranscriptionClient : ITranscriptionClient
{
    private readonly string _transcript;

    public MockTranscriptionClient(string transcript = "this is a free flow windows preview build")
        => _transcript = transcript;

    public Task<string> TranscribeAsync(AudioClip clip, TranscriptionOptions options, CancellationToken ct = default)
        => Task.FromResult(_transcript);
}
