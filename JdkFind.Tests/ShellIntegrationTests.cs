using JdkFind.Cli;

namespace JdkFind.Tests;

/// <summary>
///     Pins the shell integration outputs: the env statement per format
///     (including the single-quote escaping rules of all three shells) and
///     the init wrappers' contract — a jdkfind function that delegates
///     everything but 'env' and evaluates the sh output through it.
/// </summary>
public class ShellIntegrationTests
{
    private static Jvm JvmAt(string home) => new()
    {
        Home = new DirectoryInfo(home),
        Providers = [],
        Executable = new JavaExecutable
        {
            Path = Path.Combine(home, "bin", "java"),
            Version = JvmVersion.Parse("21.0.5"),
        },
        Version = JvmVersion.Parse("21.0.5"),
        HasCompiler = true,
    };

    [Fact]
    public void For_Posix_ExportsWithSingleQuoteEscaping()
    {
        var script = EnvScript.For(JvmAt(Path.Combine("some dir", "jdk's home")), "sh");

        // POSIX single quotes keep backslashes literal and escape the quote
        // as closer-\'-opener, independent of the path's separators.
        Assert.StartsWith("export JAVA_HOME='", script);
        Assert.EndsWith("'\n", script);
        Assert.Contains("'\\''", script);
        Assert.DoesNotContain(@"\\", script);
    }

    [Fact]
    public void For_Fish_EscapesBackslashesAndQuotes()
    {
        // A backslash in the name makes the doubling visible on every
        // platform (on Windows the directory separators carry it too).
        var script = EnvScript.For(JvmAt(Path.Combine("some dir", @"jdk\test'ing")), "fish");

        Assert.StartsWith("set -gx JAVA_HOME '", script);
        Assert.EndsWith("'\n", script);
        Assert.Contains(@"\\", script);
        Assert.Contains("\\'", script);
    }

    [Fact]
    public void For_PowerShell_DoublesTheQuote_KeepsBackslashes()
    {
        var script = EnvScript.For(JvmAt(Path.Combine("some dir", "jdk's home")), "powershell");

        Assert.StartsWith("$env:JAVA_HOME = '", script);
        Assert.EndsWith("'\n", script);
        Assert.Contains("''", script);
        Assert.DoesNotContain(@"\\", script);
    }

    [Fact]
    public void For_UnknownFormatFallsBackToPosix() =>
        Assert.StartsWith("export JAVA_HOME=", EnvScript.For(JvmAt(Path.Combine("some dir", "jdk")), "tcsh"));

    [Fact]
    public void Init_Wrappers_DelegateEverythingButEnv()
    {
        // The delegation statement differs per shell; what matters is that
        // every wrapper hands non-env invocations to the real binary.
        Assert.Contains("command jdkfind \"$@\"", ShellInit.ReadScript("bash"));
        Assert.Contains("command jdkfind \"$@\"", ShellInit.ReadScript("zsh"));
        Assert.Contains("command jdkfind $argv", ShellInit.ReadScript("fish"));
        Assert.Contains("& jdkfind.exe @args", ShellInit.ReadScript("powershell"));
    }

    [Fact]
    public void Init_Wrappers_UseTheMatchingFormats()
    {
        Assert.Contains("jdkfind env --format sh", ShellInit.ReadScript("bash"));
        Assert.Contains("jdkfind env --format sh", ShellInit.ReadScript("zsh"));
        Assert.Contains("jdkfind env --format fish", ShellInit.ReadScript("fish"));
        Assert.Contains("env --format powershell", ShellInit.ReadScript("powershell"));
    }

    [Fact]
    public void Init_Wrappers_RemoveThePreviouslyManagedBinDir()
    {
        // The PATH-accumulation guard is the one non-obvious contract: every
        // wrapper must drop the last exported bin dir before prepending.
        Assert.Contains("JDKFIND_BIN", ShellInit.ReadScript("bash"));
        Assert.Contains("JDKFIND_BIN", ShellInit.ReadScript("zsh"));
        Assert.Contains("JDKFIND_BIN", ShellInit.ReadScript("fish"));
        Assert.Contains("JDKFIND_BIN", ShellInit.ReadScript("powershell"));
    }
}
