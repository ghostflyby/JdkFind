using System.Text.Json;
using System.Text.Json.Serialization;

namespace JdkFind.Json;

/// <summary>Serializes a <see cref="DirectoryInfo" /> as its full path.</summary>
public sealed class DirectoryInfoJsonConverter : JsonConverter<DirectoryInfo>
{
    /// <summary>Writes the full path.</summary>
    public override void Write(Utf8JsonWriter writer, DirectoryInfo value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.FullName);

    /// <summary>Reads a full path.</summary>
    /// <exception cref="JsonException">The value is not a non-empty string.</exception>
    public override DirectoryInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var path = reader.GetString();
        if (string.IsNullOrEmpty(path))
            throw new JsonException("A directory path must be a non-empty string.");

        return new DirectoryInfo(path);
    }
}
