using System.Text.Json;
using System.Text.Json.Serialization;

namespace JdkFind;

/// <summary>
///     Serializes <see cref="JvmVersion" /> as its raw <c>JAVA_VERSION</c> string.
///     Reading parses the string with <see cref="JvmVersion.Parse" />, so an
///     unparseable value round-trips as an unknown placeholder instead of failing.
/// </summary>
public sealed class JvmVersionJsonConverter : JsonConverter<JvmVersion>
{
    /// <summary>Writes the raw version string.</summary>
    public override void Write(Utf8JsonWriter writer, JvmVersion value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Original);

    /// <summary>Reads a raw version string; unparseable values become unknown
    /// placeholders rather than throwing.</summary>
    public override JvmVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JvmVersion.Parse(reader.GetString());
}
