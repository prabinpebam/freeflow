using System.Text.Json;
using System.Text.Json.Serialization;
using FreeFlow.Core.Input;

namespace FreeFlow.Infrastructure.Settings;

/// <summary>
/// Serializes a <see cref="HotkeyCombination"/> as its canonical display string
/// (e.g. <c>Ctrl+Alt+Space</c>) so the settings file is human-readable and the
/// round-trip reuses the already-tested Parse/Format logic. Unreadable values
/// deserialize to <see cref="HotkeyCombination.None"/>.
/// </summary>
public sealed class HotkeyCombinationJsonConverter : JsonConverter<HotkeyCombination>
{
    public override HotkeyCombination Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString();
        if (string.IsNullOrWhiteSpace(text) || text == "(unset)")
        {
            return HotkeyCombination.None;
        }

        return HotkeyCombination.TryParse(text, out var combo) ? combo : HotkeyCombination.None;
    }

    public override void Write(Utf8JsonWriter writer, HotkeyCombination value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.IsUnset ? "(unset)" : value.Format());
}
