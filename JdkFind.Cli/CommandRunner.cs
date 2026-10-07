using System.ComponentModel;
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

        // jre/bin covers JDK 8 inner-JRE homes whose top-level bin has no java.
        var javaDirectories = new[] { bin, Path.Combine(home, "jre", "bin") };
        var pathValue = string.Join(Path.PathSeparator,
            javaDirectories.Append(Environment.GetEnvironmentVariable("PATH") ?? string.Empty));

        if (OperatingSystem.IsWindows())
        {
            // The Windows process block reflects these, and the spawned child inherits it.
            Environment.SetEnvironmentVariable("JAVA_HOME", home);
            Environment.SetEnvironmentVariable("PATH", pathValue);
            return Spawn(commandArgs);
        }

        // Environment.SetEnvironmentVariable does not reach the native environ that
        // execvp reads, so the child environment goes through libc directly.
        _ = Setenv("JAVA_HOME", home, overwrite: 1);
        _ = Setenv("PATH", pathValue, overwrite: 1);
        return Exec(commandArgs);
    }

    [LibraryImport("libc", EntryPoint = "setenv", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Setenv(string name, string value, int overwrite);

    /// <summary>Unix path: execvp replaces this process, so a successful call never
    /// returns; the remaining code only runs when the command could not be executed.</summary>
    private static int Exec(IReadOnlyList<string> commandArgs)
    {
        // execvp(3) requires a NULL-terminated argv.
        var argv = commandArgs.Append(null).ToArray();
        _ = Execvp(commandArgs[0], argv);

        var errno = Marshal.GetLastWin32Error();
        Console.Error.WriteLine(errno switch
        {
            2 => $"jdkfind: command not found: {argv[0]}",
            13 => $"jdkfind: permission denied: {argv[0]}",
            _ => $"jdkfind: cannot execute '{argv[0]}' (errno {errno})",
        });
        return errno == 2 ? ExitCommandNotFound : ExitCannotExecute;
    }

    [LibraryImport("libc", EntryPoint = "execvp", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    private static partial int Execvp(string file, string?[] argv);

    [SupportedOSPlatform("windows")]
    private static int Spawn(IReadOnlyList<string> commandArgs)
    {
        var fileName = ResolveWindows(commandArgs[0]);
        if (fileName is null)
        {
            Console.Error.WriteLine($"jdkfind: command not found: {commandArgs[0]}");
            return ExitCommandNotFound;
        }

        var startInfo = new ProcessStartInfo { UseShellExecute = false };
        if (fileName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
        {
            // Batch files are launched through the interpreter explicitly. Letting
            // CreateProcess auto-wrap them re-parses our argument line with cmd's
            // implicit quote rules, which splits args at metacharacters; with
            // /d /s /c and the whole line wrapped in an outer quote pair (stripped
            // by /S), every element keeps its own quoting and '& | < >' stay
            // inside the quotes. A literal % remains cmd-expanded — known limit.
            var comspec = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            startInfo.FileName = comspec;
            startInfo.Arguments = "/d /s /c \"" + string.Join(" ",
                commandArgs.Select(argument => '"' + argument.Replace("\"", "\"\"") + '"')) + "\"";
        }
        else
        {
            startInfo.FileName = fileName;
            foreach (var argument in commandArgs.Skip(1))
                startInfo.ArgumentList.Add(argument);
        }

        // Since .NET 8.0.4 the runtime validates batch-target arguments itself and
        // throws for combinations its own rules consider unsafe; surface that as a
        // clean error instead of a stack trace.
        try
        {
            using var process = Process.Start(startInfo)!;
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine($"jdkfind: cannot execute '{commandArgs[0]}': {exception.Message}");
            return ExitCannotExecute;
        }
        catch (Win32Exception exception)
        {
            // e.g. a PATHEXT entry that CreateProcess cannot execute (BAD_EXE_FORMAT).
            Console.Error.WriteLine($"jdkfind: cannot execute '{commandArgs[0]}': {exception.Message}");
            return ExitCannotExecute;
        }
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