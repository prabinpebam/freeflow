using System.Text.Json;
using System.Text.Json.Serialization;
using FreeFlow.Core.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FreeFlow.Infrastructure.Settings;

/// <summary>
/// File-backed <see cref="ISettingsStore"/> persisting <see cref="AppSettings"/>
/// as JSON. Provider API keys are encrypted at rest through the injected
/// <see cref="ISecretProtector"/> (DPAPI in production, a fake in tests); the
/// plaintext never touches disk. Reads are corruption-resilient per porting-plan
/// Section 8.1 — a missing or damaged file loads as <see cref="AppSettings.Defaults"/>
/// — and writes are atomic (temp file + replace) under an instance lock.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private readonly string _path;
    private readonly ISecretProtector _protector;
    private readonly ILogger<JsonSettingsStore> _logger;
    private readonly object _gate = new();

    private static readonly JsonSerializerOptions Options = CreateOptions();

    public JsonSettingsStore(string path, ISecretProtector protector, ILogger<JsonSettingsStore>? logger = null)
    {
        _path = path;
        _protector = protector;
        _logger = logger ?? NullLogger<JsonSettingsStore>.Instance;
    }

    public AppSettings Load()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
            {
                return AppSettings.Defaults;
            }

            try
            {
                var json = File.ReadAllText(_path);
                var stored = JsonSerializer.Deserialize<AppSettings>(json, Options);
                return stored is null ? AppSettings.Defaults : Unprotect(stored);
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Settings file unreadable; falling back to defaults.");
                return AppSettings.Defaults;
            }
        }
    }

    public void Save(AppSettings settings)
    {
        lock (_gate)
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(Protect(settings), Options);
            var temp = _path + ".tmp";
            File.WriteAllText(temp, json);

            if (File.Exists(_path))
            {
                File.Replace(temp, _path, null);
            }
            else
            {
                File.Move(temp, _path);
            }
        }
    }

    private AppSettings Protect(AppSettings settings)
        => MapApiKeys(settings, _protector.Protect);

    private AppSettings Unprotect(AppSettings settings)
        => MapApiKeys(settings, _protector.Unprotect);

    private static AppSettings MapApiKeys(AppSettings settings, Func<string, string> transform)
        => settings with
        {
            Providers = settings.Providers with
            {
                Transcription = settings.Providers.Transcription with
                {
                    ApiKey = transform(settings.Providers.Transcription.ApiKey),
                },
                PostProcessing = settings.Providers.PostProcessing with
                {
                    ApiKey = transform(settings.Providers.PostProcessing.ApiKey),
                },
            },
        };

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new HotkeyCombinationJsonConverter());
        return options;
    }
}
