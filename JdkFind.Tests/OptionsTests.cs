using JdkFind.Cli;

namespace JdkFind.Tests;

public class OptionsTests
{
    [Fact]
    public void Parse_NoPositionals_IsDefaultCommand() =>
        Assert.Equal(SubCommand.None, Options.Parse([]).Command);

    [Fact]
    public void Parse_SubCommands()
    {
        Assert.Equal(SubCommand.Info, Options.Parse(["info"]).Command);
        Assert.Equal(SubCommand.List, Options.Parse(["list"]).Command);
    }

    [Fact]
    public void Parse_VersionPrefix_CanAppearAloneOrWithTool()
    {
        var versionOnly = Options.Parse(["21"]);
        Assert.Equal(SubCommand.None, versionOnly.Command);
        Assert.Equal("21", versionOnly.VersionPrefix);
        Assert.Null(versionOnly.Tool);

        var withTool = Options.Parse(["21.0", "java"]);
        Assert.Equal("21.0", withTool.VersionPrefix);
        Assert.Equal("java", withTool.Tool);
    }

    [Fact]
    public void Parse_ToolAlone_IsDefaultCommandTool()
    {
        var options = Options.Parse(["java"]);

        Assert.Equal(SubCommand.None, options.Command);
        Assert.Equal("java", options.Tool);
        Assert.Null(options.VersionPrefix);
    }

    [Fact]
    public void Parse_FlagsApplyToAnyCommand()
    {
        var options = Options.Parse(["info", "--jdk-only", "--no-probe", "--vendor", "zulu", "--arch=aarch64", "--json"]);

        Assert.Equal(SubCommand.Info, options.Command);
        Assert.True(options.JdkOnly);
        Assert.True(options.NoProbe);
        Assert.Equal("zulu", options.Vendor);
        Assert.Equal("aarch64", options.Architecture);
        Assert.True(options.OutputJson);
    }

    [Fact]
    public void Parse_FlagsBeforeAndAfterPositionals_AreEqual()
    {
        var before = Options.Parse(["--jdk-only", "info", "21"]);
        var after = Options.Parse(["info", "21", "--jdk-only"]);

        Assert.Equal(before.Command, after.Command);
        Assert.Equal(before.VersionPrefix, after.VersionPrefix);
        Assert.Equal(before.JdkOnly, after.JdkOnly);
    }

    [Theory]
    [InlineData("home", "javac")]     // the home subcommand was superseded by the default command
    [InlineData("--path")]
    [InlineData("--latest")]
    [InlineData("-v", "21")]
    [InlineData("-0")]
    public void Parse_RemovedSyntax_IsRejected(params string[] args) =>
        Assert.Throws<ArgumentException>(() => Options.Parse(args));

    [Theory]
    [InlineData("info", "java")]          // info takes no tool
    [InlineData("list", "java")]
    [InlineData("21", "17")]              // two version prefixes
    [InlineData("21.0.5", "21")]          // duplicate version
    [InlineData("home", "javac", "java")] // extra positional
    [InlineData("21.0.5.1")]              // more than three segments
    [InlineData("21..5")]                 // empty segment
    [InlineData("21.x")]                  // non-numeric segment
    public void Parse_RejectsInvalidPositionals(params string[] args) =>
        Assert.Throws<ArgumentException>(() => Options.Parse(args));
}
