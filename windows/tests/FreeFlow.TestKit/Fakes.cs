using System.Security.Cryptography;
using FreeFlow.Core.Abstractions;
using FreeFlow.Core.Audio;
using FreeFlow.Core.Context;
using FreeFlow.Core.Pipeline;

namespace FreeFlow.TestKit;

/// <summary>Plays a fixed audio clip; records whether start/stop were called.</summary>
public sealed class FixtureAudioCapture : IAudioCaptureService
{
    private readonly AudioClip _clip;

    public FixtureAudioCapture(AudioClip clip) => _clip = clip;

    public bool Started { get; private set; }
    public bool Stopped { get; private set; }

    public Task StartAsync(CancellationToken ct = default)
    {
        Started = true;
        return Task.CompletedTask;
    }

    public Task<AudioClip> StopAndGetClipAsync(CancellationToken ct = default)
    {
        Stopped = true;
        return Task.FromResult(_clip);
    }
}

/// <summary>
/// Replays recorded transcripts keyed by a hash of the audio samples, with an
/// optional default response. The deterministic stand-in for a real STT call.
/// </summary>
public sealed class CassetteTranscriptionClient : ITranscriptionClient
{
    private readonly Dictionary<string, string> _byHash;
    private readonly string? _default;

    public CassetteTranscriptionClient(string transcript)
    {
        _default = transcript;
        _byHash = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    public CassetteTranscriptionClient(IDictionary<string, string> byHash, string? defaultTranscript = null)
    {
        _byHash = new Dictionary<string, string>(byHash, StringComparer.Ordinal);
        _default = defaultTranscript;
    }

    public TranscriptionOptions? LastOptions { get; private set; }

    public Task<string> TranscribeAsync(AudioClip clip, TranscriptionOptions options, CancellationToken ct = default)
    {
        LastOptions = options;
        var key = HashOf(clip);
        if (_byHash.TryGetValue(key, out var recorded))
        {
            return Task.FromResult(recorded);
        }

        if (_default is not null)
        {
            return Task.FromResult(_default);
        }

        throw new KeyNotFoundException($"No cassette for audio hash {key} and no default transcript configured.");
    }

    public static string HashOf(AudioClip clip)
        => Convert.ToHexString(SHA256.HashData(clip.Samples));
}

/// <summary>Returns a fixed cleaned string; records the request it received.</summary>
public sealed class ScriptedPostProcessingClient : IPostProcessingClient
{
    private readonly string _output;

    public ScriptedPostProcessingClient(string output) => _output = output;

    public PostProcessingRequest? LastRequest { get; private set; }

    public Task<string> CleanupAsync(PostProcessingRequest request, CancellationToken ct = default)
    {
        LastRequest = request;
        return Task.FromResult(_output);
    }
}

/// <summary>Returns a fixed captured context.</summary>
public sealed class StaticContextService : IContextService
{
    private readonly CaptureContext _context;

    public StaticContextService(CaptureContext context) => _context = context;

    public Task<CaptureContext> CaptureAsync(CancellationToken ct = default)
        => Task.FromResult(_context);
}

/// <summary>
/// In-memory clipboard + paste recorder that preserves/restores the clipboard
/// around each paste (in a finally block) so the restore invariant holds even
/// when the paste itself fails.
/// </summary>
public sealed class InMemoryClipboardPasteService : IClipboardPasteService
{
    public string? Clipboard { get; set; }
    public string? LastPasted { get; private set; }
    public List<string> PasteLog { get; } = new();
    public bool RestoreClipboard { get; set; } = true;

    /// <summary>Optional hook to simulate a paste failure for a given text.</summary>
    public Func<string, bool>? PasteShouldThrow { get; set; }

    public Task PasteTextAsync(string text, CancellationToken ct = default)
    {
        var snapshot = Clipboard;
        try
        {
            Clipboard = text;
            if (PasteShouldThrow?.Invoke(text) == true)
            {
                throw new InvalidOperationException("Simulated paste failure.");
            }

            PasteLog.Add(text);
            LastPasted = text;
        }
        finally
        {
            if (RestoreClipboard)
            {
                Clipboard = snapshot;
            }
        }

        return Task.CompletedTask;
    }
}

/// <summary>Deterministic, seeded id provider so run ids are reproducible.</summary>
public sealed class SeededIdProvider : IIdProvider
{
    private readonly int _seed;
    private int _counter;

    public SeededIdProvider(int seed = 1) => _seed = seed;

    public Guid NewId()
    {
        _counter++;
        var bytes = new byte[16];
        BitConverter.GetBytes(_seed).CopyTo(bytes, 0);
        BitConverter.GetBytes(_counter).CopyTo(bytes, 4);
        return new Guid(bytes);
    }
}
