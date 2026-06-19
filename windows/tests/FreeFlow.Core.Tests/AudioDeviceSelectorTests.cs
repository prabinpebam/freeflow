using FluentAssertions;
using FreeFlow.Core.Audio;

namespace FreeFlow.Core.Tests;

[Trait("Tier", "L0")]
public class AudioDeviceSelectorTests
{
    private static readonly string[] Devices = { "Mic A", "Headset B", "USB Webcam C" };

    [Fact]
    public void Empty_selection_resolves_to_system_default()
    {
        AudioDeviceSelector.ResolveIndex(Devices, null).Should().Be(AudioDeviceSelector.SystemDefaultIndex);
        AudioDeviceSelector.ResolveIndex(Devices, "").Should().Be(AudioDeviceSelector.SystemDefaultIndex);
        AudioDeviceSelector.ResolveIndex(Devices, "   ").Should().Be(AudioDeviceSelector.SystemDefaultIndex);
    }

    [Theory]
    [InlineData("Mic A", 0)]
    [InlineData("Headset B", 1)]
    [InlineData("USB Webcam C", 2)]
    public void Matching_device_resolves_to_its_index(string id, int expected)
        => AudioDeviceSelector.ResolveIndex(Devices, id).Should().Be(expected);

    [Fact]
    public void Unplugged_device_falls_back_to_system_default()
        => AudioDeviceSelector.ResolveIndex(Devices, "Ghost Mic")
            .Should().Be(AudioDeviceSelector.SystemDefaultIndex);

    [Fact]
    public void Match_is_case_sensitive_to_avoid_collisions()
        => AudioDeviceSelector.ResolveIndex(Devices, "mic a")
            .Should().Be(AudioDeviceSelector.SystemDefaultIndex);

    [Fact]
    public void Null_device_list_resolves_to_system_default()
        => AudioDeviceSelector.ResolveIndex(null!, "Mic A")
            .Should().Be(AudioDeviceSelector.SystemDefaultIndex);

    [Fact]
    public void System_default_sentinel_is_empty_id()
    {
        AudioInputDevice.SystemDefault.Id.Should().BeEmpty();
        AudioInputDevice.SystemDefault.IsSystemDefault.Should().BeTrue();
        new AudioInputDevice("Mic A", "Mic A").IsSystemDefault.Should().BeFalse();
    }
}
