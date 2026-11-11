using System.Text.Json;
using System.Text.Json.Serialization;

namespace JdkFind;

/// <summary>
///     Serializes <see cref="Jvm" /> to the stable machine-readable projection
///     documented by the CLI's <c>--json</c> output. The converter writes every key
///     itself, so serializer options cannot rename, omit, reorder, or add keys; it
///     also keeps <see cref="System.IO.DirectoryInfo" /> and computed members out of
///     the payload, which makes the shape safe for source-generated and AOT
///     scenarios. Deserialization is not supported: the projection does not carry
///     enough information (notably <see cref="JavaExecutable.Path" />) to rebuild a
///     <see cref="Jvm" /> without inventing data.
/// </summary>
public sealed class JvmJsonConverter : JsonConverter<Jvm>
{
    /// <summary>Writes the fixed projection. The writer's options control only
    /// presentation such as indentation and escaping, never the key set.</summary>
    public override void Write(Utf8JsonWriter writer, Jvm value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("home", value.Home.FullName);
        writer.WriteString("version", value.Version.Original);

        if (value.LanguageVersion is { } languageVersion)
            writer.WriteNumber("languageVersion", languageVersion);
        else
            writer.WriteNull("languageVersion");

        writer.WriteBoolean("hasCompiler", value.HasCompiler);
        writer.WriteBoolean("prerelease", value.Version.IsPreRelease);
        writer.WriteString("vendor", value.Vendor.ToString());
        writer.WriteString("distribution", value.Distribution.ToString());

        // The contract reports the binary's vendor with the release file as the
        // fallback (JavaExecutable.VendorRaw), not Jvm.VendorRaw's release-only
        // value. Keep the two distinct.
        writer.WriteString("vendorRaw", value.Executable.VendorRaw);
        WriteNullableString(writer, "runtimeName", value.Executable.RuntimeName);
        WriteNullableString(writer, "runtimeVersion", value.Executable.RuntimeVersion);
        writer.WriteString("vmName", value.Executable.VmName);
        writer.WriteString("vmVersion", value.Executable.VmVersion);
        writer.WriteString("architecture", value.Executable.Architecture);
        writer.WriteString("osName", value.Executable.OsName);

        writer.WriteStartArray("providers");
        foreach (var provider in value.Providers)
            writer.WriteStringValue(provider);
        writer.WriteEndArray();

        writer.WriteEndObject();
    }

    /// <summary>Not supported: the JSON projection is write-only.</summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public override Jvm Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException(
            "Jvm is a write-only JSON projection; deserialization would have to invent the executable path.");

    private static void WriteNullableString(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (value is null)
            writer.WriteNull(propertyName);
        else
            writer.WriteString(propertyName, value);
    }
}
