using System.Text.Json.Serialization;

namespace JdkFind.Cli;

/// <summary>Pre-generated metadata for the CLI's machine-readable output.
/// Indented to preserve the published presentation; the converter applied to
/// <see cref="Jvm" /> owns the key set.</summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Jvm))]
[JsonSerializable(typeof(Jvm[]))]
internal sealed partial class CliJsonContext : JsonSerializerContext;
