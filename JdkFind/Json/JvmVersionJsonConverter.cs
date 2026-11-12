using System.Text.Json;
using System.Text.Json.Serialization;

namespace JdkFind.Json;

/// <summary>Serializes <see cref="JvmVersion" /> as its raw <c>JAVA_VERSION</c> string.</summary>
public sealed class JvmVersionJsonConverter : JsonConverter<JvmVersion>
{
    /// <summary>Writes the raw version string.</summary>
    public override void Write(Utf8JsonWriter writer, JvmVersion value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Original);

    /// <summary>Reads a raw version string; unparseable values become unknown placeholders.</summary>
    public override JvmVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        JvmVersion.Parse(reader.GetString());
}
