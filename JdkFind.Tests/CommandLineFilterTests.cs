using JdkFind.Cli;

namespace JdkFind.Tests;

public class CommandLineFilterTests
{
    private static readonly Jvm AzulJvm = new()
    {
        Home = new DirectoryInfo("/jvm/zulu"),
        Providers = ["stub"],
        Executable = new JavaExecutable { Path = "/jvm/zulu/bin/java", Version = new JvmVersion(new Version(21, 0, 12, 1), false, "21.0.12.1") },
        Version = new JvmVersion(new Version(21, 0, 12, 1), false, "21.0.12.1"),
        Vendor = JvmVendor.Azul,
        Distribution = JvmDistribution.Zulu,
        VendorRaw = "Azul Systems, Inc.",
    };

    private static readonly Jvm Arm64Jvm = new()
    {
        Home = new DirectoryInfo("/jvm/temurin"),
        Providers = ["stub"],
        Executable = new JavaExecutable { Path = "/jvm/temurin/bin/java", Version = new JvmVersion(new Version(21, 0, 5, 9), false, "21.0.5+9"), Architecture = "aarch64" },
        Version = new JvmVersion(new Version(21, 0, 5, 9), false, "21.0.5+9"),
        Vendor = JvmVendor.Adoptium,
        Distribution = JvmDistribution.Temurin,
        VendorRaw = "Eclipse Adoptium",
    };

    private static readonly Jvm X64Jvm = new()
    {
        Home = new DirectoryInfo("/jvm/corretto"),
        Providers = ["stub"],
        Executable = new JavaExecutable { Path = "/jvm/corretto/bin/java", Version = new JvmVersion(new Version(17, 0, 9, 1), false, "17.0.9.1"), Architecture = "x86_64" },
        Version = new JvmVersion(new Version(17, 0, 9, 1), false, "17.0.9.1"),
        Vendor = JvmVendor.Amazon,
        Distribution = JvmDistribution.Corretto,
        VendorRaw = "Amazon Corretto",
    };

    [Theory]
    [InlineData("azul")]          // normalized vendor hit
    [InlineData("Azul Systems")]  // raw string hit
    [InlineData("AZUL")]          // case-insensitive
    public void MatchesVendorFilter_MatchesVendorAndRawString(string text) =>
        Assert.True(CommandLine.MatchesVendorFilter(AzulJvm, text));

    [Fact]
    public void MatchesVendorFilter_NullOrEmptyText_PassesEverything()
    {
        Assert.True(CommandLine.MatchesVendorFilter(AzulJvm, null));
        Assert.True(CommandLine.MatchesVendorFilter(AzulJvm, string.Empty));
    }

    [Fact]
    public void MatchesVendorFilter_UnknownText_Rejects() =>
        Assert.False(CommandLine.MatchesVendorFilter(AzulJvm, "corretto"));

    [Fact]
    public void MatchesDistributionFilter_MatchesFoojayName()
    {
        Assert.True(CommandLine.MatchesDistributionFilter(AzulJvm, "zulu"));
        Assert.True(CommandLine.MatchesDistributionFilter(AzulJvm, "Zulu"));
        Assert.False(CommandLine.MatchesDistributionFilter(AzulJvm, "corretto"));
        Assert.True(CommandLine.MatchesDistributionFilter(AzulJvm, null));
    }

    [Theory]
    [InlineData("aarch64")]  // exact spelling
    [InlineData("arm64")]    // group alias
    [InlineData("ARM64")]    // case-insensitive alias
    [InlineData("aarch")]    // non-member text falls through to the plain substring match
    public void MatchesArchFilter_Arm64Spellings_MatchAarch64Installation(string text) =>
        Assert.True(CommandLine.MatchesArchFilter(Arm64Jvm, text));

    [Theory]
    [InlineData("x86_64")]  // exact spelling
    [InlineData("amd64")]   // group alias
    [InlineData("x64")]     // group alias
    [InlineData("x86")]     // non-member text falls through to the plain substring match
    public void MatchesArchFilter_X64Spellings_MatchX86_64Installation(string text) =>
        Assert.True(CommandLine.MatchesArchFilter(X64Jvm, text));

    [Theory]
    [InlineData("amd64")]
    [InlineData("x64")]
    [InlineData("x86_64")]
    public void MatchesArchFilter_X64Texts_RejectAarch64Installation(string text) =>
        Assert.False(CommandLine.MatchesArchFilter(Arm64Jvm, text));

    [Theory]
    [InlineData("arm64")]
    [InlineData("aarch64")]
    public void MatchesArchFilter_Arm64Texts_RejectX86_64Installation(string text) =>
        Assert.False(CommandLine.MatchesArchFilter(X64Jvm, text));

    [Fact]
    public void MatchesArchFilter_NullOrEmptyText_PassesEverything()
    {
        Assert.True(CommandLine.MatchesArchFilter(Arm64Jvm, null));
        Assert.True(CommandLine.MatchesArchFilter(Arm64Jvm, string.Empty));
    }

    [Fact]
    public void MatchesArchFilter_MissingArchitecture_RejectsAnyText()
    {
        Assert.False(CommandLine.MatchesArchFilter(AzulJvm, "aarch64"));
        Assert.False(CommandLine.MatchesArchFilter(AzulJvm, "riscv64"));
    }

    [Fact]
    public void MatchesArchFilter_EmptyArchitecture_RejectsAnyText()
    {
        var empty = new Jvm
        {
            Home = new DirectoryInfo("/jvm/empty"),
            Providers = ["stub"],
            Executable = new JavaExecutable { Path = "/jvm/empty/bin/java", Version = new JvmVersion(new Version(21, 0, 0, 0), false, "21") },
            Version = new JvmVersion(new Version(21, 0, 0, 0), false, "21"),
        };
        Assert.False(CommandLine.MatchesArchFilter(empty, "aarch64"));
        Assert.False(CommandLine.MatchesArchFilter(empty, "riscv64"));
    }

    [Fact]
    public void MatchesArchFilter_UppercaseArchitecture_MatchesAliasCaseInsensitively()
    {
        var upper = new Jvm
        {
            Home = new DirectoryInfo("/jvm/upper"),
            Providers = ["stub"],
            Executable = new JavaExecutable { Path = "/jvm/upper/bin/java", Version = new JvmVersion(new Version(21, 0, 0, 0), false, "21"), Architecture = "AARCH64" },
            Version = new JvmVersion(new Version(21, 0, 0, 0), false, "21"),
        };
        Assert.True(CommandLine.MatchesArchFilter(upper, "arm64"));
        Assert.False(CommandLine.MatchesArchFilter(upper, "amd64"));
    }
}
