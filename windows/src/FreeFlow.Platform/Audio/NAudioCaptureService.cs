using FreeFlow.Core.Abstractions;
using FreeFlow.Core.Audio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NAudio.Wave;

namespace FreeFlow.Platform.Audio;

/// <summary>
/// Real microphone capture adapter built on NAudio's <see cref="WaveInEvent"/>.
/// Records mono 16 kHz PCM16 — the format the transcription pipeline expects —
/// and returns the buffered samples as an <see cref="AudioClip"/>. The input
/// device is resolved from the user's saved selection at the start of each
/// recording (falling back to the OS default when unset or unplugged). Device
/// I/O is exercised by the L4/L5 tiers; the deterministic loop uses fixture audio.
/// </summary>
public sealed class NAudioCaptureService : IAudioCaptureService, IDisposable
{
    private readonly ILogger<NAudioCaptureService> _logger;
    private readonly Func<string?>? _selectedDeviceId;
    private readonly int _sampleRate;
    private readonly int _channels;

    private WaveInEvent? _waveIn;
    private MemoryStream? _buffer;
    private TaskCompletionSource<bool>? _stopped;

    public NAudioCaptureService(
        ILogger<NAudioCaptureService>? logger = null,
        Func<string?>? selectedDeviceId = null,
        int sampleRate = 16000,
        int channels = 1)
    {
        _logger = logger ?? NullLogger<NAudioCaptureService>.Instance;
        _selectedDeviceId = selectedDeviceId;
        _sampleRate = sampleRate;
        _channels = channels;
    }

    public Task StartAsync(CancellationToken ct = default)
    {
        Cleanup();

        _buffer = new MemoryStream();
        _stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var deviceNumber = ResolveDeviceNumber();
        _waveIn = new WaveInEvent
        {
            DeviceNumber = deviceNumber,
            WaveFormat = new WaveFormat(_sampleRate, 16, _channels),
            BufferMilliseconds = 50,
        };

        _waveIn.DataAvailable += OnDataAvailable;
        _waveIn.RecordingStopped += OnRecordingStopped;

        _logger.LogDebug(
            "Starting capture at {Rate} Hz, {Channels} channel(s) on device {Device}.",
            _sampleRate, _channels, deviceNumber);
        _waveIn.StartRecording();
        return Task.CompletedTask;
    }

    private int ResolveDeviceNumber()
    {
        var selectedId = _selectedDeviceId?.Invoke();
        if (string.IsNullOrWhiteSpace(selectedId))
        {
            return AudioDeviceSelector.SystemDefaultIndex;
        }

        return AudioDeviceSelector.ResolveIndex(NAudioDeviceProvider.EnumerateDeviceIds(), selectedId);
    }

    public async Task<AudioClip> StopAndGetClipAsync(CancellationToken ct = default)
    {
        if (_waveIn is null || _buffer is null || _stopped is null)
        {
            return AudioClip.Empty;
        }

        _waveIn.StopRecording();
        await _stopped.Task.ConfigureAwait(false); // wait for the final DataAvailable flush

        var samples = _buffer.ToArray();
        _logger.LogDebug("Captured {Bytes} bytes ({Ms} ms).", samples.Length, samples.Length / 2 / _channels * 1000 / _sampleRate);

        Cleanup();
        return new AudioClip(samples, _sampleRate, _channels);
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
        => _buffer?.Write(e.Buffer, 0, e.BytesRecorded);

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            _logger.LogError(e.Exception, "Recording stopped with error.");
        }

        _stopped?.TrySetResult(true);
    }

    private void Cleanup()
    {
        if (_waveIn is not null)
        {
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.RecordingStopped -= OnRecordingStopped;
            _waveIn.Dispose();
            _waveIn = null;
        }

        _buffer?.Dispose();
        _buffer = null;
        _stopped = null;
    }

    public void Dispose() => Cleanup();
}
