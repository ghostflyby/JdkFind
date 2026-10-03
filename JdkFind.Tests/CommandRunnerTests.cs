using System.Diagnostics;

namespace JdkFind.Tests;

/// <summary>
///     End-to-end tests for the '-- command' execution mode: they spawn the real
///     tool apphost (jdkfind from the test output directory) as a child process,
///     exercising the Unix exec and Windows spawn paths as deployed. Requires at
///     least one JDK on the machine (CI runners ship one — same assumption as the
///     package smoke test).
/// </summary>
public class CommandRunnerTests
{
    private static (int ExitCode, string StdOut) RunTool(params string[] args)
    {
        // The apphost is launched directly: the `dotnet` driver consumes the '--'
        // separator, which would mask the very grammar under test.
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "jdkfind.exe" : "jdkfind"),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        foreach (var argument in args)
            startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo)!;
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, stdout);
    }

    [Fact]
    public void Run_PropagatesTheChildExitCode()
    {
        var (exitCode, _) = OperatingSystem.IsWindows()
            ? RunTool("--", "cmd", "/c", "exit 7")
            : RunTool("--", "sh", "-c", "exit 7");

        Assert.Equal(7, exitCode);
    }

    [Fact]
    public void Run_SetsJavaHome_AndPrependsBinToPath()
    {
        // The selected installation's bin directory must be the first java on PATH,
        // and JAVA_HOME must point at the same home.
        var (exitCode, javaHome) = OperatingSystem.IsWindows()
            ? RunTool("--", "cmd", "/c", "echo %JAVA_HOME%")
            : RunTool("--", "sh", "-c", "printf '%s' \"$JAVA_HOME\"");

        Assert.Equal(0, exitCode);
        Assert.False(string.IsNullOrWhiteSpace(javaHome));
        Assert.True(Directory.Exists(Path.Combine(javaHome.Trim(), "bin")));

        var (_, javaOnPath) = OperatingSystem.IsWindows()
            ? RunTool("--", "cmd", "/c", "where java")
            : RunTool("--", "sh", "-c", "command -v java");

        Assert.StartsWith(Path.Combine(javaHome.Trim(), "bin"), javaOnPath.Trim());
    }

    [Fact]
    public void Run_MissingCommand_Returns127()
    {
        var (exitCode, _) = RunTool("--", "jdkfind-no-such-command-xyz");

        Assert.Equal(127, exitCode);
    }
}
