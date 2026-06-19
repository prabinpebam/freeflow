using FluentAssertions;
using FreeFlow.Core.Input;
using FreeFlow.Core.Providers;
using FreeFlow.Core.Settings;
using FreeFlow.Infrastructure.Settings;
using FreeFlow.TestKit;

namespace FreeFlow.Pipeline.Tests;

[Trait("Tier", "L1")]
public class JsonSettingsStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;
    private readonly FakeSecretProtector _protector = new();

    public JsonSettingsStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "freeflow-tests", Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_dir, "settings.json");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { /* best effort */ }
    }

    private JsonSettingsStore Store() => new(_path, _protector);

    private static AppSettings Sample() => AppSettings.Defaults with
    {
        Providers = new ProviderConfiguration
        {
            Transcription = new ProviderSettings { Model = "whisper-large-v3", ApiKey = "sk-secret-t" },
            PostProcessing = new ProviderSettings { Model = "llama-3.3-70b-versatile", ApiKey = "sk-secret-p" },
        },
        Hotkeys = HotkeyBindings.Defaults with { Toggle = HotkeyCombination.Parse("Ctrl+Shift+D") },
        General = new GeneralSettings { LaunchAtLogin = true, HistoryCap = 50 },
    };

    [Fact]
    public void Load_returns_defaults_when_file_missing()
    {
        Store().Load().Should().Be(AppSettings.Defaults);
    }

    [Fact]
    public void Save_then_load_round_trips_with_plaintext_keys()
    {
        var store = Store();
        store.Save(Sample());

        var loaded = store.Load();

        loaded.Providers.Transcription.ApiKey.Should().Be("sk-secret-t");
        loaded.Providers.PostProcessing.ApiKey.Should().Be("sk-secret-p");
        loaded.Hotkeys.Toggle.Format().Should().Be("Ctrl+Shift+D");
        loaded.General.LaunchAtLogin.Should().BeTrue();
        loaded.General.HistoryCap.Should().Be(50);
    }

    [Fact]
    public void Api_keys_are_encrypted_on_disk_not_plaintext()
    {
        Store().Save(Sample());

        var onDisk = File.ReadAllText(_path);

        onDisk.Should().NotContain("sk-secret-t");
        onDisk.Should().NotContain("sk-secret-p");
        onDisk.Should().Contain("enc:");
    }

    [Fact]
    public void Corrupt_file_loads_as_defaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_path, "{ this is not valid json");

        Store().Load().Should().Be(AppSettings.Defaults);
    }

    [Fact]
    public void Save_overwrites_existing_file_atomically()
    {
        var store = Store();
        store.Save(Sample());
        store.Save(Sample() with { General = new GeneralSettings { HistoryCap = 7 } });

        store.Load().General.HistoryCap.Should().Be(7);
        Directory.GetFiles(_dir).Should().ContainSingle();
    }
}
