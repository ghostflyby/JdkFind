using System.Text.Json;
using System.Text.Json.Serialization;

namespace JdkFind;

/// <summary>
///     Serializes <see cref="Jvm" /> to the stable machine-readable projection
///     documented by the CLI's <c>--json</c> output. The key set lives in the
///     internal <see cref="JvmJsonModel" />, whose source-generated metadata is
///     fixed, so serializer options cannot rename, omit, reorder, or add keys. The
///     model keeps <see cref="System.IO.DirectoryInfo" /> and computed members out
///     of the payload, which makes the shape safe for source-generated and AOT
///     scenarios. Deserialization is not supported: the projection does not carry
///     enough information (notably <see cref="JavaExecutable.Path" />) to rebuild a
///     <see cref="Jvm" /> without inventing data.
/// </summary>
public sealed class JvmJsonConverter : JsonConverter<Jvm>
{
    /// <summary>Writes the fixed projection by serializing the internal wire model.
    /// The writer's options control only presentation such as indentation and
    /// escaping, never the key set.</summary>
    public override void Write(Utf8JsonWriter writer, Jvm value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, new JvmJsonModel(value), JvmJsonModelContext.Default.JvmJsonModel);

    /// <summary>Not supported: the JSON projection is write-only.</summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public override Jvm Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException(
            "Jvm is a write-only JSON projection; deserialization would have to invent the executable path.");
}
