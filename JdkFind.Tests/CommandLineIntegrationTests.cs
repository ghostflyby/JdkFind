using JdkFind.Cli;

namespace JdkFind.Tests;

/// <summary>
///     Integration tests driving the real CommandLine.Run entry point with the
///     console streams captured over controlled fixtures — pinning exit codes,
///     stream discipline and the ExitNotFound contract.
/// </summary>
public class CommandLineIntegrationTests : IDisposable
{
    private readonly TempDirectory temp = new();

    private sealed class StubJvmProvider(string name, params string[] homes) : IJvmProvider
    {
        public string Name { get; } = name;

        public IEnumerable<string> GetJavaHomes() => homes;
    }

    private JdkFindOptions StubOptions(params string[] homes) =>
        new() { Providers = [new StubJvmProvider("stub", homes)], ProbeRuntimeProperties = false };

    private static (int ExitCode, string StdOut, string StdErr) RunCli(string[] args, JdkFindOptions findOptions)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try
        {
            var exitCode = CommandLine.Run(args, findOptions);
            return (exitCode, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void DefaultCommand_PrintsSelectedHomeOnStdout()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "jdk");

        var (exitCode, stdout, stderr) = RunCli([], StubOptions(jdk));

        Assert.Equal(0, exitCode);
        Assert.Equal(jdk + Environment.NewLine, stdout);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public void VersionPositional_SelectsTheMatchingInstallation()
    {
        var jdk21 = TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "jdk21");
        var jdk17 = TestJdk.Create(temp.FullPath, "17.0.2", "jdks", "jdk17");

        var (_, stdout, _) = RunCli(["21"], StubOptions(jdk21, jdk17));

        Assert.Equal(jdk21 + Environment.NewLine, stdout);
    }

    [Fact]
    public void ToolPositional_PrintsTheBinExecutablePath()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "jdk");

        var (_, stdout, _) = RunCli(["21", "java"], StubOptions(jdk));

        Assert.Equal(Path.Combine(jdk, "bin", JavaHomeLayout.JavaExecutableName) + Environment.NewLine, stdout);
    }

    [Fact]
    public void InfoCommand_WritesDetailsToStderr_KeepsStdoutEmpty()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "jdk");

        var (exitCode, stdout, stderr) = RunCli(["info"], StubOptions(jdk));

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains($"home: {jdk}", stderr);
        Assert.Contains("version: 21.0.5 (feature 21)", stderr);
        Assert.Contains("type: jre", stderr);
    }

    [Fact]
    public void ListCommand_WritesTableToStderr_KeepsStdoutEmpty()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "jdk");

        var (exitCode, stdout, stderr) = RunCli(["list"], StubOptions(jdk));

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("VERSION", stderr);
        Assert.Contains(jdk, stderr);
    }

    [Fact]
    public void JsonCommand_WritesSingleObjectToStdout()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "jdk");

        var (exitCode, stdout, stderr) = RunCli(["--json"], StubOptions(jdk));

        Assert.Equal(0, exitCode);
        Assert.StartsWith("{", stdout);
        Assert.Contains("\"home\"", stdout);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public void ListJson_WritesAnArrayToStdout()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "jdk");

        var (_, stdout, _) = RunCli(["list", "--json"], StubOptions(jdk));

        Assert.StartsWith("[", stdout);
        Assert.EndsWith("]" + Environment.NewLine, stdout);
    }

    [Fact]
    public void NoMatches_ReturnsExitNotFoundWithEmptyStreams()
    {
        var (exitCode, stdout, stderr) = RunCli([], StubOptions());

        Assert.Equal(1, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public void UsageError_ReturnsExit2WithMessageOnStderr()
    {
        var (exitCode, stdout, stderr) = RunCli(["--bogus"], StubOptions());

        Assert.Equal(2, exitCode);
        Assert.Equal(string.Empty, stdout);
        Assert.Contains("--bogus", stderr);
    }

    [Fact]
    public void Help_WritesUsageToStdout_ReturnsZero()
    {
        var (exitCode, stdout, stderr) = RunCli(["--help"], StubOptions());

        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", stdout);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public void JdkOnly_DropsRuntimeOnlyInstallations()
    {
        var jdk = TestJdk.Create(temp.FullPath, "21.0.5", "jdks", "jdk");
        File.WriteAllText(Path.Combine(jdk, "bin", JavaHomeLayout.CompilerExecutableName), string.Empty);
        var runtime = TestJdk.Create(temp.FullPath, "17.0.2", "jdks", "jre");
        var options = StubOptions(jdk, runtime);

        var jdkOnly = RunCli(["--jdk-only"], options);

        Assert.Equal(0, jdkOnly.ExitCode);
        Assert.Equal(jdk + Environment.NewLine, jdkOnly.StdOut);

        // list is the only multi-installation output: the table goes to stderr
        // (1 header row + 2 data rows).
        var listed = RunCli(["list"], options);
        Assert.Equal(string.Empty, listed.StdOut);
        Assert.Contains("VERSION", listed.StdErr);
        Assert.Contains(jdk, listed.StdErr);
        Assert.Contains(runtime, listed.StdErr);
    }

    [Fact]
    public void VendorFilter_HitsDisplayAndRawSurfaces()
    {
        var jdk = TestJdk.CreateWithImplementor(temp.FullPath, "21.0.5", "Azul Systems, Inc.", "jdks", "azul");
        var options = StubOptions(jdk);

        Assert.Equal(0, RunCli(["--vendor", "azul"], options).ExitCode);
        Assert.Equal(1, RunCli(["--vendor", "nomatch"], options).ExitCode);
    }

    public void Dispose() => temp.Dispose();
}
