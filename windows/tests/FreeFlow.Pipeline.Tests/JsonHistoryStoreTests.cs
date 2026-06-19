using FluentAssertions;
using FreeFlow.Core.History;
using FreeFlow.Infrastructure.History;

namespace FreeFlow.Pipeline.Tests;

[Trait("Tier", "L1")]
public class JsonHistoryStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public JsonHistoryStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "freeflow-tests", Guid.NewGuid().ToString("N"));
        _path = Path.Combine(_dir, "history.json");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { /* best effort */ }
    }

    private static HistoryEntry Entry(string raw) => new()
    {
        Id = Guid.NewGuid(),
        Timestamp = DateTimeOffset.UtcNow,
        Intent = "dictation",
        RawTranscript = raw,
        PostProcessedTranscript = raw + ".",
    };

    [Fact]
    public async Task Missing_store_loads_empty()
    {
        var store = new JsonHistoryStore(_path);
        (await store.LoadAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Add_then_load_returns_newest_first()
    {
        var store = new JsonHistoryStore(_path);
        await store.AddAsync(Entry("first"));
        await store.AddAsync(Entry("second"));

        var entries = await store.LoadAsync();
        entries.Should().HaveCount(2);
        entries[0].RawTranscript.Should().Be("second");
        entries[1].RawTranscript.Should().Be("first");
    }

    [Fact]
    public async Task Persists_across_instances()
    {
        await new JsonHistoryStore(_path).AddAsync(Entry("persisted"));

        var reopened = await new JsonHistoryStore(_path).LoadAsync();
        reopened.Should().ContainSingle().Which.RawTranscript.Should().Be("persisted");
    }

    [Fact]
    public async Task Enforces_retention_cap()
    {
        var store = new JsonHistoryStore(_path, cap: 3);
        for (var i = 0; i < 10; i++)
        {
            await store.AddAsync(Entry($"entry-{i}"));
        }

        var entries = await store.LoadAsync();
        entries.Should().HaveCount(3);
        entries[0].RawTranscript.Should().Be("entry-9");
        entries[2].RawTranscript.Should().Be("entry-7");
    }

    [Fact]
    public async Task Corrupt_file_loads_empty_and_recovers_on_write()
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(_path, "{ this is not valid json ]");

        var store = new JsonHistoryStore(_path);
        (await store.LoadAsync()).Should().BeEmpty();

        await store.AddAsync(Entry("recovered"));
        (await store.LoadAsync()).Should().ContainSingle().Which.RawTranscript.Should().Be("recovered");
    }

    [Fact]
    public async Task Clear_empties_the_store()
    {
        var store = new JsonHistoryStore(_path);
        await store.AddAsync(Entry("a"));
        await store.ClearAsync();
        (await store.LoadAsync()).Should().BeEmpty();
    }
}
