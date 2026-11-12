using System.Text.Json.Serialization;

namespace JdkFind.Cli;

/// <summary>Pre-generated metadata for the CLI's machine-readable output.</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(JvmInfo))]
[JsonSerializable(typeof(JvmInfo[]))]
internal sealed partial class CliJsonContext : JsonSerializerContext;
