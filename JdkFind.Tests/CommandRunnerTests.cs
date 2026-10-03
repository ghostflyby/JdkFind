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
        // Drained so a chatty child can never fill the stderr pipe and deadlock.
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        return (process.ExitCode, stdout);
    }

    [Fact]
    public void Run_NonExecutableCommand_Returns126()
    {
        if (OperatingSystem.IsWindows())
            return; // The Unix path reports the executable bit; PATHEXT has no such concept.

        var file = Path.Combine(Path.GetTempPath(), $"jdkfind-{Guid.NewGuid():N}.sh");
        File.WriteAllText(file, "#!/bin/sh\nexit 0\n");
        File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite); // not executable

        var (exitCode, _) = RunTool("--", file);

        Assert.Equal(126, exitCode);
    }

    [Fact]
    public void Run_PassesMetacharactersVerbatim()
    {
        if (OperatingSystem.IsWindows())
            return; // The metachar assertion uses a POSIX shell.

        var (_, output) = RunTool("--", "sh", "-c", "printf '<%s>' \"$1\"", "metachar", "a b|c;d$&");

        Assert.Equal("<a b|c;d$&>", output);
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

    [Fact]
    public void Run_BatchArguments_SurviveCmdMetacharacters()
    {
        if (!OperatingSystem.IsWindows())
            return; // Batch targets are a Windows concept.

        var bat = Path.Combine(Path.GetTempPath(), $"jdkfind-{Guid.NewGuid():N}.bat");
        // echo cannot output an argument containing '&' (cmd re-parses the expanded
        // line), so the comparison goes through an exit code instead.
        File.WriteAllText(bat, "@if \"%~1 %~2\" == \"a&b with spaces\" (exit /b 0)\r\n@exit /b 44");

        try
        {
            // 'a&b' without spaces is unquoted by default .NET quoting; the batch
            // quoting added for bat targets must keep it as one argument.
            var (exitCode, output) = RunTool("--", bat, "a&b", "with spaces");
            Assert.Equal(0, exitCode);

            var lines = output.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(["a&b", "with spaces"], lines);
        }
        finally
        {
            File.Delete(bat);
        }
    }
}
