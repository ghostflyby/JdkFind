using System.Text.Json;
using System.Text.Json.Serialization;
using JdkFind.Cli;

namespace JdkFind.Tests;

/// <summary>
///     Pins the --json output contract. Breaking this test is a breaking change
///     of the tool's public surface.
/// </summary>
public class JvmJsonContractTests
{
    private static Jvm FullyPopulated => new()
    {
        Home = new DirectoryInfo("/jvm/zulu"),
        Providers = ["macos"],
        Executable = new JavaExecutable
        {
            Path = "/jvm/zulu/bin/java",
            Home = new DirectoryInfo("/jvm/zulu"),
            Version = JvmVersion.Parse("21.0.12.1"),
            VendorRaw = "Azul Systems, Inc.",
            Vendor = JvmVendor.Azul,
            OsName = "Darwin",
            Architecture = "aarch64",
            RuntimeName = "OpenJDK Runtime Environment",
            RuntimeVersion = "21.0.12.1+1-LTS",
            VmName = "OpenJDK 64-Bit Server VM",
            VmVersion = "21.0.12.1+1-LTS",
        },
        Version = JvmVersion.Parse("21.0.12.1"),
        LanguageVersion = 21,
        HasCompiler = true,
        VendorRaw = "Azul Systems, Inc.",
        Vendor = JvmVendor.Azul,
        Distribution = JvmDistribution.Zulu,
    };

    private static Jvm UnknownValues => new()
    {
        Home = new DirectoryInfo("/jvm/unknown"),
        Providers = [],
        Executable = new JavaExecutable
        {
            Path = "/jvm/unknown/bin/java",
            Version = JvmVersion.Unknown(),
            VendorRaw = string.Empty,
            OsName = string.Empty,
            Architecture = string.Empty,
            RuntimeName = null,
            RuntimeVersion = null,
            VmName = string.Empty,
            VmVersion = string.Empty,
        },
        Version = JvmVersion.Unknown(),
        LanguageVersion = null,
        HasCompiler = false,
        VendorRaw = null,
        Vendor = JvmVendor.Unknown,
        Distribution = JvmDistribution.Unknown,
    };

    [Fact]
    public void Shape_ContainsExactlyTheContractedKeys_WithContractedTypes()
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(FullyPopulated, TestJsonContext.Default.Jvm));
        var root = doc.RootElement;

        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal(FullyPopulated.Home.FullName, root.GetProperty("home").GetString());
        Assert.Equal("21.0.12.1", root.GetProperty("version").GetString());
        Assert.Equal(21, root.GetProperty("languageVersion").GetInt32());
        Assert.True(root.GetProperty("hasCompiler").GetBoolean());
        Assert.False(root.GetProperty("prerelease").GetBoolean());
        Assert.Equal("Azul", root.GetProperty("vendor").GetString());
        Assert.Equal("Zulu", root.GetProperty("distribution").GetString());
        Assert.Equal("Azul Systems, Inc.", root.GetProperty("vendorRaw").GetString());
        Assert.Equal(1, root.GetProperty("providers").GetArrayLength());

        var expected = new (string Name, JsonValueKind Kind)[]
        {
            ("home", JsonValueKind.String),
            ("providers", JsonValueKind.Array),
            ("executable", JsonValueKind.Object),
            ("version", JsonValueKind.String),
            ("languageVersion", JsonValueKind.Number),
            ("hasCompiler", JsonValueKind.True),
            ("prerelease", JsonValueKind.False),
            ("vendorRaw", JsonValueKind.String),
            ("vendor", JsonValueKind.String),
            ("distribution", JsonValueKind.String),
        };

        var actual = root.EnumerateObject().Select(p => (p.Name, p.Value.ValueKind)).ToArray();
        Assert.Equal(expected, actual);

        var executable = root.GetProperty("executable");
        var expectedExecutable = new (string Name, JsonValueKind Kind)[]
        {
            ("path", JsonValueKind.String),
            ("home", JsonValueKind.String),
            ("startFailure", JsonValueKind.Null),
            ("version", JsonValueKind.String),
            ("vendorRaw", JsonValueKind.String),
            ("vendor", JsonValueKind.String),
            ("osName", JsonValueKind.String),
            ("architecture", JsonValueKind.String),
            ("runtimeName", JsonValueKind.String),
            ("runtimeVersion", JsonValueKind.String),
            ("vmName", JsonValueKind.String),
            ("vmVersion", JsonValueKind.String),
        };

        var actualExecutable = executable.EnumerateObject().Select(p => (p.Name, p.Value.ValueKind)).ToArray();
        Assert.Equal(expectedExecutable, actualExecutable);
        Assert.Equal("/jvm/zulu/bin/java", executable.GetProperty("path").GetString());
        Assert.Equal(FullyPopulated.Executable.Home!.FullName, executable.GetProperty("home").GetString());
        Assert.False(executable.TryGetProperty("installation", out _));
    }

    [Fact]
    public void Shape_UnknownValues_KeepEveryKeyWithNullsOrEmptyStrings()
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(UnknownValues, TestJsonContext.Default.Jvm));
        var root = doc.RootElement;

        Assert.Equal(10, root.EnumerateObject().Count());
        Assert.Equal(JsonValueKind.String, root.GetProperty("version").ValueKind);
        Assert.Equal(string.Empty, root.GetProperty("version").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("languageVersion").ValueKind);
        Assert.False(root.GetProperty("prerelease").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("vendorRaw").ValueKind);
        Assert.Equal(0, root.GetProperty("providers").GetArrayLength());
        Assert.Equal("Unknown", root.GetProperty("vendor").GetString());
        Assert.Equal("Unknown", root.GetProperty("distribution").GetString());

        var executable = root.GetProperty("executable");
        Assert.Equal(12, executable.EnumerateObject().Count());
        Assert.Equal(JsonValueKind.Null, executable.GetProperty("home").ValueKind);
        Assert.Equal(string.Empty, executable.GetProperty("version").GetString());
        Assert.Equal(string.Empty, executable.GetProperty("vendorRaw").GetString());
        Assert.Equal("Unknown", executable.GetProperty("vendor").GetString());
        Assert.Equal(JsonValueKind.Null, executable.GetProperty("runtimeName").ValueKind);
        Assert.Equal(JsonValueKind.Null, executable.GetProperty("runtimeVersion").ValueKind);
        Assert.Equal(string.Empty, executable.GetProperty("vmName").GetString());
        Assert.Equal(string.Empty, executable.GetProperty("vmVersion").GetString());
        Assert.Equal(string.Empty, executable.GetProperty("architecture").GetString());
        Assert.Equal(string.Empty, executable.GetProperty("osName").GetString());
        Assert.Equal(JsonValueKind.Null, executable.GetProperty("startFailure").ValueKind);
    }

    [Fact]
    public void VendorAndVersion_Provenance_IsSeparatedByTheNestedObject()
    {
        var jvm = FullyPopulated with
        {
            Version = JvmVersion.Parse("21.0.11"),
            VendorRaw = "release vendor",
            Vendor = JvmVendor.Oracle,
            Executable = FullyPopulated.Executable with
            {
                Version = JvmVersion.Parse("21.0.12.1"),
                VendorRaw = "probe vendor",
                Vendor = JvmVendor.Azul,
            },
        };

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(jvm, TestJsonContext.Default.Jvm));
        var root = doc.RootElement;
        var executable = root.GetProperty("executable");

        Assert.Equal("21.0.11", root.GetProperty("version").GetString());
        Assert.Equal("release vendor", root.GetProperty("vendorRaw").GetString());
        Assert.Equal("Oracle", root.GetProperty("vendor").GetString());

        Assert.Equal("21.0.12.1", executable.GetProperty("version").GetString());
        Assert.Equal("probe vendor", executable.GetProperty("vendorRaw").GetString());
        Assert.Equal("Azul", executable.GetProperty("vendor").GetString());
    }

    [Fact]
    public void JvmInfo_FlattensBothTypesIntoTheFlatContract()
    {
        var json = JsonSerializer.Serialize(JvmInfo.From(FullyPopulated), TestJsonContext.Default.JvmInfo);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var expected = new (string Name, JsonValueKind Kind)[]
        {
            ("home", JsonValueKind.String),
            ("version", JsonValueKind.String),
            ("languageVersion", JsonValueKind.Number),
            ("hasCompiler", JsonValueKind.True),
            ("prerelease", JsonValueKind.False),
            ("vendor", JsonValueKind.String),
            ("distribution", JsonValueKind.String),
            ("vendorRaw", JsonValueKind.String),
            ("runtimeName", JsonValueKind.String),
            ("runtimeVersion", JsonValueKind.String),
            ("vmName", JsonValueKind.String),
            ("vmVersion", JsonValueKind.String),
            ("architecture", JsonValueKind.String),
            ("osName", JsonValueKind.String),
            ("providers", JsonValueKind.Array),
        };

        var actual = root.EnumerateObject().Select(p => (p.Name, p.Value.ValueKind)).ToArray();
        Assert.Equal(expected, actual);
        Assert.Equal(FullyPopulated.Home.FullName, root.GetProperty("home").GetString());
        Assert.Equal("21.0.12.1", root.GetProperty("version").GetString());
        Assert.Equal(21, root.GetProperty("languageVersion").GetInt32());
        Assert.Equal("Azul", root.GetProperty("vendor").GetString());
        Assert.Equal("Zulu", root.GetProperty("distribution").GetString());
        Assert.Equal("Azul Systems, Inc.", root.GetProperty("vendorRaw").GetString());
        Assert.Equal("OpenJDK Runtime Environment", root.GetProperty("runtimeName").GetString());
        Assert.Equal("aarch64", root.GetProperty("architecture").GetString());
        Assert.Equal(1, root.GetProperty("providers").GetArrayLength());
    }

    [Fact]
    public void JvmInfo_SerializesExactlyLikeTheLegacyFlatContract()
    {
        var legacy = new LegacyJvmDto(
            FullyPopulated.Home.FullName,
            FullyPopulated.Version.Original,
            FullyPopulated.LanguageVersion,
            FullyPopulated.HasCompiler,
            FullyPopulated.Version.IsPreRelease,
            FullyPopulated.Vendor.ToString(),
            FullyPopulated.Distribution.ToString(),
            FullyPopulated.Executable.VendorRaw,
            FullyPopulated.Executable.RuntimeName,
            FullyPopulated.Executable.RuntimeVersion,
            FullyPopulated.Executable.VmName,
            FullyPopulated.Executable.VmVersion,
            FullyPopulated.Executable.Architecture,
            FullyPopulated.Executable.OsName,
            FullyPopulated.Providers);

        var legacyJson = JsonSerializer.Serialize(legacy, LegacyJsonContext.Default.LegacyJvmDto);
        var currentJson = JsonSerializer.Serialize(JvmInfo.From(FullyPopulated), LegacyJsonContext.Default.JvmInfo);

        Assert.Equal(legacyJson, currentJson);
    }

    [Fact]
    public void JvmInfo_VendorRaw_ComesFromTheExecutable()
    {
        var jvm = FullyPopulated with
        {
            VendorRaw = "release vendor",
            Executable = FullyPopulated.Executable with { VendorRaw = "probe vendor" },
        };

        Assert.Equal("probe vendor", JvmInfo.From(jvm).VendorRaw);
    }

    [Fact]
    public void JvmInfo_RoundTrips()
    {
        var info = JvmInfo.From(FullyPopulated);
        var json = JsonSerializer.Serialize(info, TestJsonContext.Default.JvmInfo);
        var roundTripped = JsonSerializer.Deserialize(json, TestJsonContext.Default.JvmInfo);

        Assert.NotNull(roundTripped);
        Assert.Equal(info.Home.FullName, roundTripped.Home.FullName);
        Assert.Equal(info.Version, roundTripped.Version);
        Assert.Equal(info.LanguageVersion, roundTripped.LanguageVersion);
        Assert.Equal(info.HasCompiler, roundTripped.HasCompiler);
        Assert.Equal(info.Prerelease, roundTripped.Prerelease);
        Assert.Equal(info.Vendor, roundTripped.Vendor);
        Assert.Equal(info.Distribution, roundTripped.Distribution);
        Assert.Equal(info.VendorRaw, roundTripped.VendorRaw);
        Assert.Equal(info.RuntimeName, roundTripped.RuntimeName);
        Assert.Equal(info.RuntimeVersion, roundTripped.RuntimeVersion);
        Assert.Equal(info.VmName, roundTripped.VmName);
        Assert.Equal(info.VmVersion, roundTripped.VmVersion);
        Assert.Equal(info.Architecture, roundTripped.Architecture);
        Assert.Equal(info.OsName, roundTripped.OsName);
        Assert.Equal(info.Providers, roundTripped.Providers);
    }

    [Fact]
    public void Prerelease_True_RoundTrips()
    {
        var jvm = FullyPopulated with
        {
            Version = JvmVersion.Parse("25-ea"),
            LanguageVersion = 25,
        };

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(jvm, TestJsonContext.Default.Jvm));

        Assert.True(doc.RootElement.GetProperty("prerelease").GetBoolean());
        Assert.Equal("25-ea", doc.RootElement.GetProperty("version").GetString());
    }

    [Fact]
    public void JvmVersion_SerializesAsItsRawStringAndRoundTrips()
    {
        var version = JvmVersion.Parse("21.0.5+9-LTS");

        var json = JsonSerializer.Serialize(version, TestJsonContext.Default.JvmVersion);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("21.0.5+9-LTS", doc.RootElement.GetString());
        Assert.Equal(version, JsonSerializer.Deserialize(json, TestJsonContext.Default.JvmVersion));
    }

    [Fact]
    public void JvmVersion_UnparseableValue_RoundTripsAsUnknown()
    {
        var version = JvmVersion.Parse("not-a-version");

        var json = JsonSerializer.Serialize(version, TestJsonContext.Default.JvmVersion);

        using var doc = JsonDocument.Parse(json);
        Assert.Equal("not-a-version", doc.RootElement.GetString());
        Assert.Equal(version, JsonSerializer.Deserialize(json, TestJsonContext.Default.JvmVersion));
    }

    [Fact]
    public void VendorAndDistribution_SerializeAsTheirNames()
    {
        Assert.Equal("\"Azul\"", JsonSerializer.Serialize(JvmVendor.Azul, TestJsonContext.Default.JvmVendor));
        Assert.Equal("\"Zulu\"", JsonSerializer.Serialize(JvmDistribution.Zulu, TestJsonContext.Default.JvmDistribution));
    }

    [Fact]
    public void JavaExecutable_SerializesStandaloneWithItsHome()
    {
        var json = JsonSerializer.Serialize(FullyPopulated.Executable, TestJsonContext.Default.JavaExecutable);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.Equal("/jvm/zulu/bin/java", root.GetProperty("path").GetString());
        Assert.Equal(FullyPopulated.Executable.Home!.FullName, root.GetProperty("home").GetString());
        Assert.False(root.TryGetProperty("installation", out _));
    }

    [Fact]
    public void ArrayAndListMetadata_ShareTheSameProjection()
    {
        var array = JsonSerializer.Serialize([FullyPopulated], TestJsonContext.Default.JvmArray);
        var list = JsonSerializer.Serialize<IReadOnlyList<Jvm>>([FullyPopulated], TestJsonContext.Default.IReadOnlyListJvm);

        Assert.Equal(array, list);
    }

    [Fact]
    public void CliContext_IsIndentedAndUsesTheFlatProjection()
    {
        var json = JsonSerializer.Serialize(JvmInfo.From(FullyPopulated), CliJsonContext.Default.JvmInfo);

        Assert.Contains(Environment.NewLine, json);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(15, doc.RootElement.EnumerateObject().Count());
        Assert.False(doc.RootElement.TryGetProperty("executable", out _));
    }

    [Fact]
    public void Jvm_RoundTripsThroughTheNestedShape()
    {
        var json = JsonSerializer.Serialize(FullyPopulated, TestJsonContext.Default.Jvm);
        var roundTripped = JsonSerializer.Deserialize(json, TestJsonContext.Default.Jvm);

        Assert.NotNull(roundTripped);
        Assert.Equal(FullyPopulated.Home.FullName, roundTripped.Home.FullName);
        Assert.Equal(FullyPopulated.Providers, roundTripped.Providers);
        Assert.Equal(FullyPopulated.Version, roundTripped.Version);
        Assert.Equal(FullyPopulated.LanguageVersion, roundTripped.LanguageVersion);
        Assert.Equal(FullyPopulated.HasCompiler, roundTripped.HasCompiler);
        Assert.Equal(FullyPopulated.VendorRaw, roundTripped.VendorRaw);
        Assert.Equal(FullyPopulated.Vendor, roundTripped.Vendor);
        Assert.Equal(FullyPopulated.Distribution, roundTripped.Distribution);

        var executable = roundTripped.Executable;
        Assert.Equal(FullyPopulated.Executable.Path, executable.Path);
        Assert.Equal(FullyPopulated.Executable.Version, executable.Version);
        Assert.Equal(FullyPopulated.Executable.VendorRaw, executable.VendorRaw);
        Assert.Equal(FullyPopulated.Executable.Vendor, executable.Vendor);
        Assert.Equal(FullyPopulated.Executable.OsName, executable.OsName);
        Assert.Equal(FullyPopulated.Executable.Architecture, executable.Architecture);
        Assert.Equal(FullyPopulated.Executable.RuntimeName, executable.RuntimeName);
        Assert.Equal(FullyPopulated.Executable.RuntimeVersion, executable.RuntimeVersion);
        Assert.Equal(FullyPopulated.Executable.VmName, executable.VmName);
        Assert.Equal(FullyPopulated.Executable.VmVersion, executable.VmVersion);
        Assert.Null(executable.StartFailure);
        Assert.NotNull(executable.Home);
        Assert.Equal(FullyPopulated.Executable.Home!.FullName, executable.Home.FullName);
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Jvm))]
[JsonSerializable(typeof(JvmInfo))]
[JsonSerializable(typeof(Jvm[]))]
[JsonSerializable(typeof(IReadOnlyList<Jvm>))]
[JsonSerializable(typeof(JavaExecutable))]
[JsonSerializable(typeof(JvmVersion))]
[JsonSerializable(typeof(JvmVendor))]
[JsonSerializable(typeof(JvmDistribution))]
internal sealed partial class TestJsonContext : JsonSerializerContext;

internal sealed record LegacyJvmDto(
    string Home,
    string Version,
    int? LanguageVersion,
    bool HasCompiler,
    bool Prerelease,
    string Vendor,
    string Distribution,
    string? VendorRaw,
    string? RuntimeName,
    string? RuntimeVersion,
    string? VmName,
    string? VmVersion,
    string? Architecture,
    string? OsName,
    IReadOnlyList<string> Providers);

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(JvmInfo))]
[JsonSerializable(typeof(LegacyJvmDto))]
internal sealed partial class LegacyJsonContext : JsonSerializerContext;
