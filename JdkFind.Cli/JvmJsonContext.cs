using System.Text.Json.Serialization;

namespace JdkFind.Cli;

/// <summary>JSON boundary type: <see cref="Jvm" /> with primitives only, so the
/// generated serializer never walks <see cref="DirectoryInfo" /> (its <c>Parent</c>
/// chain recurses without bound).</summary>
internal sealed record JvmDto(
    string Home,
    string? Version,
    int? LanguageVersion,
    string? Vendor,
    string? Architecture,
    string? OsName,
    string Provider);

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(JvmDto[]))]
internal sealed partial class JvmJsonContext : JsonSerializerContext;
