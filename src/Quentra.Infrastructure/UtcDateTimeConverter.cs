using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Quentra.Infrastructure;

// A date without an offset would be read as the machine's local time, so the same file would hash
// differently in another time zone. Offsets are required, and every date is held in UTC so the same
// instant written as Z or +05:30 hashes identically.
public sealed partial class UtcDateTimeConverter : JsonConverter<DateTimeOffset>
{
    public const string MissingOffset = "date and time has no UTC offset; end it with Z or an offset such as +05:30 (for example 2026-09-27T10:00:00Z)";

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String || !reader.TryGetDateTimeOffset(out var value))
            throw new JsonException(); // The serializer's own message names the type; the CLI restates it.
        if (!Offset().IsMatch(reader.GetString()!)) throw new JsonException(MissingOffset);
        return value.ToUniversalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToUniversalTime());

    [GeneratedRegex(@"(?:[Zz]|[+-]\d{2}:?\d{2})$")]
    private static partial Regex Offset();
}
