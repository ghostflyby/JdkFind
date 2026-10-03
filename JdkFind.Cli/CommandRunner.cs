using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace JdkFind.Cli;

/// <summary>
///     Runs a command against a selected installation: JAVA_HOME is set and the
///     installation's bin directory is prepended to PATH. On Unix the command
///     replaces this process via execvp; on Windows it is spawned as a child and
///     awaited, returning the child's exit code.
/// </summary>
internal static partial class CommandRunner
{
    private const int ExitCommandNotFound = 127;
    private const int ExitCannotExecute = 126;

    internal static int RunCommand(Jvm jvm, IReadOnlyList<string> commandArgs)
    {
        var home = jvm.Home.FullName;
        var bin = Path.Combine(home, "bin");

        if (OperatingSystem.IsWindows())
        {
            // The Windows process block reflects these, and the spawned child inherits it.
            Environment.SetEnvironmentVariable("JAVA_HOME", home);
            Environment.SetEnvironmentVariable("PATH", bin + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH"));
            return Spawn(commandArgs);
        }

        // Environment.SetEnvironmentVariable does not reach the native environ that
        // execvp reads, so the child environment goes through libc directly.
        setenv("JAVA_HOME", home, overwrite: 1);
        setenv("PATH", bin + Path.PathSeparator + (Environment.GetEnvironmentVariable("PATH") ?? string.Empty), overwrite: 1);
        return Exec(commandArgs);
    }

    [LibraryImport("libc", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int setenv(string name, string value, int overwrite);

    /// <summary>Unix path: execvp replaces this process, so a successful call never
    /// returns; the remaining code only runs when the command could not be executed.</summary>
    private static int Exec(IReadOnlyList<string> commandArgs)
    {
        // execvp(3) requires a NULL-terminated argv.
        var argv = commandArgs.Append(null).ToArray();
        execvp(commandArgs[0], argv);

        var errno = Marshal.GetLastWin32Error();
        Console.Error.WriteLine(errno switch
        {
            2 => $"jdkfind: command not found: {argv[0]}",
            13 => $"jdkfind: permission denied: {argv[0]}",
            _ => $"jdkfind: cannot execute '{argv[0]}' (errno {errno})",
        });
        return errno == 2 ? ExitCommandNotFound : ExitCannotExecute;
    }

    [LibraryImport("libc", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int execvp(string file, string?[] argv);

    [SupportedOSPlatform("windows")]
    private static int Spawn(IReadOnlyList<string> commandArgs)
    {
        var fileName = ResolveWindows(commandArgs[0]);
        if (fileName is null)
        {
            Console.Error.WriteLine($"jdkfind: command not found: {commandArgs[0]}");
            return ExitCommandNotFound;
        }

        var startInfo = new ProcessStartInfo { FileName = fileName, UseShellExecute = false };
        if (fileName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
        {
            // The loader runs batch targets through cmd.exe, which re-parses the
            // command line with its own (not MSVCRT) rules: each argument is quoted
            // and embedded quotes doubled so metacharacters stay inside the quotes.
            // (A literal % cannot be protected on a cmd command line — known limit.)
            startInfo.Arguments = string.Join(" ", commandArgs.Skip(1)
                .Select(argument => '"' + argument.Replace("\"", "\"\"") + '"'));
        }
        else
        {
            foreach (var argument in commandArgs.Skip(1))
                startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        process.WaitForExit();
        return process.ExitCode;
    }

    /// <summary>Resolves a command to an executable file the way a Windows shell
    /// would: a command carrying a directory is probed in place, a bare name is
    /// searched across the already-updated PATH with PATHEXT extensions.</summary>
    [SupportedOSPlatform("windows")]
    private static string? ResolveWindows(string command)
    {
        var extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var directories = Path.IsPathRooted(command) || command.Contains('/') || command.Contains('\\')
            ? [Path.GetDirectoryName(Path.GetFullPath(command))!]
            : (Environment.GetEnvironmentVariable("PATH") ?? "")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return directories
            .SelectMany(directory => extensions
                .Select(extension => Path.Combine(directory, command + extension))
                .Append(Path.Combine(directory, command)))
            .FirstOrDefault(File.Exists);
    }
}
