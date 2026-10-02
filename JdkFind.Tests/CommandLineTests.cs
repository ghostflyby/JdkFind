using JdkFind.Cli;

namespace JdkFind.Tests;

public class CommandLineTests
{
    // Path.GetFullPath normalizes separators per OS, so the fixture works on Windows too.
    private static readonly string HomePath = Path.GetFullPath("/jvm/zulu");

    private static readonly Jvm ZuluJvm = new()
    {
        Home = new DirectoryInfo(HomePath),
        Providers = ["macos"],
        Version = new JvmVersion(new Version(21, 0, 12, 1), false, "21.0.12.1"),
        LanguageVersion = 21,
        HasCompiler = true,
        Vendor = JvmVendor.Azul,
        Distribution = JvmDistribution.Zulu,
        VendorRaw = "Azul Systems, Inc.",
        RuntimeName = "OpenJDK Runtime Environment",
        RuntimeVersion = "21.0.12+44",
        VmName = "OpenJDK 64-Bit Server VM",
        VmVersion = "21.0.12+44",
        Architecture = "aarch64",
    };

    [Theory]
    [InlineData("21", true)]
    [InlineData("21.0", true)]
    [InlineData("21.0.12", true)]
    [InlineData("21.0.12.1", true)]
    [InlineData("22", false)]
    [InlineData("21.0.13", false)]
    [InlineData("1.8", false)]
    public void MatchesVersion_MatchesSegmentPrefixes(string prefix, bool expected) =>
        Assert.Equal(expected, CommandLine.MatchesVersion(ZuluJvm, prefix));

    [Fact]
    public void ToolPath_ReturnsHomeWithoutTool() =>
        Assert.Equal(HomePath, CommandLine.ToolPath(ZuluJvm, null));

    [Fact]
    public void ToolPath_AppendsBinAndPlatformSuffix()
    {
        var path = CommandLine.ToolPath(ZuluJvm, "javac");
        var expected = Path.Combine(HomePath, "bin", OperatingSystem.IsWindows() ? "javac.exe" : "javac");

        Assert.Equal(expected, path);
    }

    [Fact]
    public void WriteInfo_EmitsOneEntryPerLine()
    {
        var writer = new StringWriter();

        CommandLine.WriteInfo(ZuluJvm, writer);

        Assert.Equal(
            [
                $"home: {HomePath}",
                "version: 21.0.12.1 (feature 21)",
                "vendor: Azul",
                "distribution: Zulu",
                "runtime: OpenJDK Runtime Environment 21.0.12+44",
                "vm: OpenJDK 64-Bit Server VM 21.0.12+44",
                "arch: aarch64",
                "type: jdk",
                "providers: macos",
            ],
            writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }
}
