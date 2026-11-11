using System.Text.Json;
using System.Text.Json.Serialization;
using JdkFind.Cli;

namespace JdkFind.Tests;

/// <summary>
///     Pins the --json output contract: every key always present, camelCase,
///     null for values that are genuinely unknown, and an empty string for the
///     fields whose backing API is non-nullable. Breaking this test is a breaking
///     change of the tool's public surface.
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
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(FullyPopulated, JvmJson.TypeInfo));
        var root = doc.RootElement;

        Assert.Equal(JsonValueKind.Object, root.ValueKind);

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
    }

    [Fact]
    public void Shape_UnknownValues_KeepEveryKeyWithNullsOrEmptyStrings()
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(UnknownValues, JvmJson.TypeInfo));
        var root = doc.RootElement;

        Assert.Equal(15, root.EnumerateObject().Count());
        Assert.Equal(JsonValueKind.String, root.GetProperty("version").ValueKind);
        Assert.Equal(string.Empty, root.GetProperty("version").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("languageVersion").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("runtimeName").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("runtimeVersion").ValueKind);
        Assert.Equal(string.Empty, root.GetProperty("vendorRaw").GetString());
        Assert.Equal(string.Empty, root.GetProperty("vmName").GetString());
        Assert.Equal(string.Empty, root.GetProperty("vmVersion").GetString());
        Assert.Equal(string.Empty, root.GetProperty("architecture").GetString());
        Assert.Equal(string.Empty, root.GetProperty("osName").GetString());
        Assert.Equal(0, root.GetProperty("providers").GetArrayLength());
        Assert.Equal("Unknown", root.GetProperty("vendor").GetString());
        Assert.Equal("Unknown", root.GetProperty("distribution").GetString());
    }

    [Fact]
    public void VendorRaw_ComesFromTheExecutable_NotTheReleaseFileProjection()
    {
        var jvm = FullyPopulated with
        {
            VendorRaw = "release-file vendor",
            Executable = FullyPopulated.Executable with { VendorRaw = "probe vendor" },
        };

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(jvm, JvmJson.TypeInfo));

        Assert.Equal("probe vendor", doc.RootElement.GetProperty("vendorRaw").GetString());
    }

    [Fact]
    public void Prerelease_True_RoundTrips()
    {
        var jvm = FullyPopulated with
        {
            Version = JvmVersion.Parse("25-ea"),
            LanguageVersion = 25,
        };

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(jvm, JvmJson.TypeInfo));

        Assert.True(doc.RootElement.GetProperty("prerelease").GetBoolean());
        Assert.Equal("25-ea", doc.RootElement.GetProperty("version").GetString());
    }

    [Fact]
    public void ArrayAndListMetadata_ShareTheSameProjection()
    {
        var array = JsonSerializer.Serialize(new[] { FullyPopulated }, JvmJson.ArrayTypeInfo);
        var list = JsonSerializer.Serialize<IReadOnlyList<Jvm>>([FullyPopulated], JvmJson.ListTypeInfo);

        Assert.Equal(array, list);
    }

    [Fact]
    public void CliContext_IsIndentedAndUsesTheSameProjection()
    {
        var json = JsonSerializer.Serialize(FullyPopulated, CliJsonContext.Default.Jvm);

        Assert.Contains(Environment.NewLine, json);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(15, doc.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public void SerializerOptions_CannotChangeTheShape()
    {
        var baseline = JsonSerializer.Serialize(FullyPopulated, JvmJson.TypeInfo);
        var context = new TestJsonContext(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.Preserve,
        });

        var altered = JsonSerializer.Serialize(FullyPopulated, context.Jvm);

        Assert.Equal(baseline, altered);
    }

    [Fact]
    public void Deserialize_IsNotSupported()
    {
        Assert.Throws<NotSupportedException>(() =>
            JsonSerializer.Deserialize<Jvm>("{}", JvmJson.TypeInfo));
    }

    [Fact]
    public void CustomOptions_CanIndentWithoutChangingKeys()
    {
        var context = new TestJsonContext(new JsonSerializerOptions { WriteIndented = true });

        var json = JsonSerializer.Serialize(FullyPopulated, context.Jvm);

        Assert.Contains(Environment.NewLine, json);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(15, doc.RootElement.EnumerateObject().Count());
    }
}

[JsonSerializable(typeof(Jvm))]
internal sealed partial class TestJsonContext : JsonSerializerContext;
