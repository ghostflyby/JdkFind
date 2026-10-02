using System.Text.Json;
using JdkFind.Cli;

namespace JdkFind.Tests;

/// <summary>
///     Pins the --json output contract: every key always present, camelCase,
///     null for unknown values. Breaking this test is a breaking change of the
///     tool's public surface.
/// </summary>
public class JvmJsonContractTests
{
    private static readonly JvmDto FullyPopulated = new(
        Home: "/jvm/zulu",
        Version: "21.0.12.1",
        LanguageVersion: 21,
        HasCompiler: true,
        Prerelease: false,
        Vendor: "Azul",
        Distribution: "Zulu",
        VendorRaw: "Azul Systems, Inc.",
        RuntimeName: "OpenJDK Runtime Environment",
        RuntimeVersion: "21.0.12.1+1-LTS",
        VmName: "OpenJDK 64-Bit Server VM",
        VmVersion: "21.0.12.1+1-LTS",
        Architecture: "aarch64",
        OsName: "Darwin",
        Providers: ["macos"]);

    [Fact]
    public void Shape_ContainsExactlyTheContractedKeys_WithContractedTypes()
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(FullyPopulated, JvmJsonContext.Default.JvmDto));
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
    public void Shape_UnknownValues_SerializeAsNullNotMissing()
    {
        var dto = new JvmDto(
            Home: "/jvm/unknown",
            Version: string.Empty,
            LanguageVersion: null,
            HasCompiler: false,
            Prerelease: false,
            Vendor: "Unknown",
            Distribution: "Unknown",
            VendorRaw: null,
            RuntimeName: null,
            RuntimeVersion: null,
            VmName: null,
            VmVersion: null,
            Architecture: null,
            OsName: null,
            Providers: ["stub"]);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(dto, JvmJsonContext.Default.JvmDto));
        var root = doc.RootElement;

        Assert.Equal(15, root.EnumerateObject().Count());
        Assert.Equal(JsonValueKind.String, root.GetProperty("version").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("languageVersion").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("vendorRaw").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("runtimeName").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("vmVersion").ValueKind);
        Assert.Equal("Unknown", root.GetProperty("vendor").GetString());
    }

    [Fact]
    public void Prerelease_True_RoundTrips()
    {
        var dto = new JvmDto("/jvm/ea", "25-ea", 25, false, true, "Oracle", "OracleOpenJdk", "Oracle Corporation",
            null, null, null, null, null, null, ["stub"]);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(dto, JvmJsonContext.Default.JvmDto));

        Assert.True(doc.RootElement.GetProperty("prerelease").GetBoolean());
        Assert.Equal("25-ea", doc.RootElement.GetProperty("version").GetString());
    }
}
