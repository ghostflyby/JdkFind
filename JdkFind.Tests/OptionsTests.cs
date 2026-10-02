using JdkFind.Cli;

namespace JdkFind.Tests;

public class OptionsTests
{
    [Theory]
    [InlineData("-p")]
    [InlineData("--path")]
    [InlineData("--paths")]
    public void Parse_PathFlag_SetsPathsOnly(string arg) =>
        Assert.True(Options.Parse([arg]).PathsOnly);

    [Fact]
    public void Parse_SetsIndividualFlags()
    {
        Assert.True(Options.Parse(["-j"]).OutputJson);
        Assert.True(Options.Parse(["--json"]).OutputJson);
        Assert.True(Options.Parse(["-l"]).Latest);
        Assert.True(Options.Parse(["--latest"]).Latest);
        Assert.True(Options.Parse(["--no-probe"]).NoProbe);
        Assert.True(Options.Parse(["--jdk-only"]).JdkOnly);
        Assert.True(Options.Parse(["-h"]).ShowHelp);
        Assert.True(Options.Parse(["--help"]).ShowHelp);
    }

    [Theory]
    [InlineData("-v", "21")]
    [InlineData("--version", "21")]
    [InlineData("--version=21")]
    public void Parse_Version_AcceptsInlineAndSeparateValues(params string[] args) =>
        Assert.Equal(21, Options.Parse(args).LanguageVersion);

    [Fact]
    public void Parse_AcceptsFilterValues()
    {
        var options = Options.Parse(["--vendor", "temurin", "--arch=aarch64"]);

        Assert.Equal("temurin", options.Vendor);
        Assert.Equal("aarch64", options.Architecture);
    }

    [Fact]
    public void Parse_RepeatedVersion_LastValueWins()
    {
        var options = Options.Parse(["-v", "8", "--version=21"]);

        Assert.Equal(21, options.LanguageVersion);
    }

    [Theory]
    [InlineData("-v", "abc")]
    [InlineData("-v", "-1")]
    [InlineData("-v")]
    [InlineData("--vendor")]
    [InlineData("--bogus")]
    [InlineData("bare")]
    public void Parse_RejectsInvalidInput(params string[] args) =>
        Assert.Throws<ArgumentException>(() => Options.Parse(args));
}
