using JdkFind.Cli;
using JdkFind.Providers;

namespace JdkFind.Tests;

public class CommandLineFilterTests
{
    private static readonly Jvm AzulJvm = new()
    {
        Home = new DirectoryInfo("/jvm/zulu"),
        Providers = ["stub"],
        Version = new JvmVersion(new Version(21, 0, 12, 1), false, "21.0.12.1"),
        Vendor = JvmVendor.Azul,
        Distribution = JvmDistribution.Zulu,
        VendorRaw = "Azul Systems, Inc.",
    };

    [Theory]
    [InlineData("azul")]          // 规范化厂商命中
    [InlineData("Azul Systems")]  // 原始串命中
    [InlineData("AZUL")]          // 大小写不敏感
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
}
