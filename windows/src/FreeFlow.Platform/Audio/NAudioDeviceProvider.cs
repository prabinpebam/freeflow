using FreeFlow.Core.Abstractions;
using FreeFlow.Core.Audio;
using NAudio.Wave;

namespace FreeFlow.Platform.Audio;

/// <summary>
/// Lists the microphones visible to NAudio's WaveIn API, in the same order the
/// capture service enumerates them, so the device index resolved by
/// <see cref="AudioDeviceSelector"/> matches the index NAudio records from. The
/// device's product name is used as the stable id persisted in settings. A
/// leading "System default" entry lets the user defer to the OS default.
/// </summary>
public sealed class NAudioDeviceProvider : IAudioDeviceProvider
{
    public IReadOnlyList<AudioInputDevice> GetInputDevices()
    {
        var devices = new List<AudioInputDevice> { AudioInputDevice.SystemDefault };

        for (var i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            var name = WaveInEvent.GetCapabilities(i).ProductName;
            devices.Add(new AudioInputDevice(name, name));
        }

        return devices;
    }

    /// <summary>
    /// The device ids in WaveIn enumeration order (excluding the system-default
    /// sentinel), used to resolve a saved selection to a WaveIn device number.
    /// </summary>
    internal static IReadOnlyList<string> EnumerateDeviceIds()
    {
        var ids = new List<string>(WaveInEvent.DeviceCount);
        for (var i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            ids.Add(WaveInEvent.GetCapabilities(i).ProductName);
        }

        return ids;
    }
}
