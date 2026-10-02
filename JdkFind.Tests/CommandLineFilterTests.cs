using JdkFind.Cli;
using JdkFind.Providers;

namespace JdkFind.Tests;

public class CommandLineFilterTests
{
    private static readonly Jvm AzulJvm = new()
    {
        Home = new DirectoryInfo("/jvm/zulu"),
        Providers = ["stub"],
        Version = new JvmVersion(new Version(21, 0, 12, 1), false, "21.0.12.1"),        Vendor = "Azul Systems, Inc.",
        KnownVendor = JvmVendor.Azul,
        VendorDisplayName = "Azul Zulu",
    };

    [Theory]
    [InlineData("azul")]          // 原始串命中
    [InlineData("Azul Systems")]  // 原始串前缀
    [InlineData("zulu")]          // 展示名命中
    [InlineData("AZUL")]          // 大小写不敏感
    public void MatchesVendorFilter_HitsEveryVendorSurface(string text) =>
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
}
