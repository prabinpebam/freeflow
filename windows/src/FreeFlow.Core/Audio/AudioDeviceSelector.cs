namespace FreeFlow.Core.Audio;

/// <summary>
/// A selectable audio input (microphone) device. <paramref name="Id"/> is a
/// stable identifier persisted in settings; <paramref name="Name"/> is the
/// human-readable label shown in the UI. An empty <paramref name="Id"/> denotes
/// the system default capture device.
/// </summary>
public sealed record AudioInputDevice(string Id, string Name)
{
    /// <summary>The sentinel entry representing the OS default microphone.</summary>
    public static readonly AudioInputDevice SystemDefault = new(string.Empty, "System default");

    /// <summary>True when this entry represents the OS default capture device.</summary>
    public bool IsSystemDefault => string.IsNullOrEmpty(Id);
}

/// <summary>
/// Pure resolution of a persisted microphone selection to a capture device
/// index. Kept free of any audio-stack dependency so the matching rules are
/// deterministically unit-tested; the platform adapter supplies the live device
/// list and applies the resulting index to NAudio.
/// </summary>
public static class AudioDeviceSelector
{
    /// <summary>Index meaning "use the system default capture device".</summary>
    public const int SystemDefaultIndex = -1;

    /// <summary>
    /// Map a saved device id to its 0-based position in <paramref name="deviceIds"/>.
    /// Returns <see cref="SystemDefaultIndex"/> when the selection is empty or the
    /// saved device is no longer present (e.g. it was unplugged), so capture
    /// always falls back to the OS default instead of failing.
    /// </summary>
    public static int ResolveIndex(IReadOnlyList<string> deviceIds, string? selectedId)
    {
        if (deviceIds is null || string.IsNullOrWhiteSpace(selectedId))
        {
            return SystemDefaultIndex;
        }

        for (var i = 0; i < deviceIds.Count; i++)
        {
            if (string.Equals(deviceIds[i], selectedId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return SystemDefaultIndex;
    }
}
