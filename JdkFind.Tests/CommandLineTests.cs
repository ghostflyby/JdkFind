using System.Text;
using JdkFind.Cli;

namespace JdkFind.Tests;

public class CommandLineTests
{
    private static readonly Jvm ZuluJvm = new()
    {
        Home = new DirectoryInfo("/jvm/zulu"),
        Providers = ["macos"],
        Version = new JvmVersion(new Version(21, 0, 12, 1), false, "21.0.12.1"),
        LanguageVersion = 21,
        HasCompiler = true,
        Vendor = "Azul Systems, Inc.",
        KnownVendor = JvmVendor.Azul,
        VendorDisplayName = "Azul Zulu",
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
        Assert.Equal("/jvm/zulu", CommandLine.ToolPath(ZuluJvm, null));

    [Fact]
    public void ToolPath_AppendsBinAndPlatformSuffix()
    {
        var path = CommandLine.ToolPath(ZuluJvm, "javac");
        var expected = Path.Combine("/jvm/zulu", "bin", OperatingSystem.IsWindows() ? "javac.exe" : "javac");

        Assert.Equal(expected, path);
    }

    [Fact]
    public void WriteInfo_EmitsOneEntryPerLine()
    {
        var writer = new StringWriter();

        CommandLine.WriteInfo(ZuluJvm, writer);

        Assert.Equal(
            [
                "home: /jvm/zulu",
                "version: 21.0.12.1 (feature 21)",
                "vendor: Azul Zulu (Azul)",
                "runtime: OpenJDK Runtime Environment 21.0.12+44",
                "vm: OpenJDK 64-Bit Server VM 21.0.12+44",
                "arch: aarch64",
                "type: jdk",
                "providers: macos",
            ],
            writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }
}
