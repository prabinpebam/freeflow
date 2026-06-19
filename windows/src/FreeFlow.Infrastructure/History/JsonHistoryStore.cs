using System.Text.Json;
using FreeFlow.Core.History;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FreeFlow.Infrastructure.History;

/// <summary>
/// File-backed run history persisted as a single JSON array (newest first),
/// capped to a fixed number of entries. Corruption-resilient per porting-plan
/// Section 8.1: a missing or damaged file loads as empty and is overwritten on
/// the next write, never throwing on startup. Writes are atomic (temp file +
/// replace) and serialized through an async gate.
/// </summary>
public sealed class JsonHistoryStore : IHistoryStore
{
    private readonly string _path;
    private readonly int _cap;
    private readonly ILogger<JsonHistoryStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public JsonHistoryStore(string path, int cap = 200, ILogger<JsonHistoryStore>? logger = null)
    {
        _path = path;
        _cap = cap < 1 ? 1 : cap;
        _logger = logger ?? NullLogger<JsonHistoryStore>.Instance;
    }

    public async Task AddAsync(HistoryEntry entry, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var entries = ReadUnlocked();
            entries.Insert(0, entry);
            if (entries.Count > _cap)
            {
                entries.RemoveRange(_cap, entries.Count - _cap);
            }

            await WriteUnlockedAsync(entries, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<HistoryEntry>> LoadAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return ReadUnlocked();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await WriteUnlockedAsync(new List<HistoryEntry>(), ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private List<HistoryEntry> ReadUnlocked()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new List<HistoryEntry>();
            }

            var json = File.ReadAllText(_path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<HistoryEntry>();
            }

            var entries = JsonSerializer.Deserialize<List<HistoryEntry>>(json, Options);
            return entries ?? new List<HistoryEntry>();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "History store at {Path} was unreadable; treating as empty.", _path);
            return new List<HistoryEntry>();
        }
    }

    private async Task WriteUnlockedAsync(List<HistoryEntry> entries, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var json = JsonSerializer.Serialize(entries, Options);
        var temp = _path + ".tmp";
        await File.WriteAllTextAsync(temp, json, ct).ConfigureAwait(false);

        // Atomic-ish replace so a crash mid-write can't truncate the live file.
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
