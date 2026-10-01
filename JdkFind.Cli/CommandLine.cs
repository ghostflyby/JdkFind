using System.Text.Json;

namespace JdkFind.Cli;

internal static class CommandLine
{
    private const int ExitSuccess = 0;
    private const int ExitNotFound = 1;
    private const int ExitUsage = 2;

    internal static async Task<int> RunAsync(string[] args)
    {
        Options options;
        try
        {
            options = Options.Parse(args);
        }
        catch (ArgumentException exception)
        {
            await Console.Error.WriteLineAsync(exception.Message);
            await Console.Error.WriteLineAsync("Run 'jdkfind --help' for usage.");
            return ExitUsage;
        }

        if (options.ShowHelp)
        {
            PrintHelp();
            return ExitSuccess;
        }

        var jvms = new List<Jvm>();
        await foreach (var jvm in JdkFinder.LocateAsync())
        {
            if (options.LanguageVersion is { } version && jvm.LanguageVersion != version)
                continue;
            if (options.Vendor is { } vendor && jvm.Vendor?.Contains(vendor, StringComparison.OrdinalIgnoreCase) != true)
                continue;
            if (options.Architecture is { } architecture &&
                jvm.Architecture?.Contains(architecture, StringComparison.OrdinalIgnoreCase) != true)
                continue;

            jvms.Add(jvm);
        }

        if (jvms.Count == 0)
            return ExitNotFound;

        if (options.Latest)
        {
            Jvm? newest = null;
            foreach (var jvm in jvms)
                if (JvmVersionComparer.Default.Compare(jvm, newest) > 0)
                    newest = jvm;

            jvms = [newest!];
        }

        if (options.PathsOnly)
        {
            foreach (var jvm in jvms)
                Console.WriteLine(jvm.Home.FullName);
        }
        else if (options.OutputJson)
        {
            var dtos = jvms.Select(jvm => new JvmDto(
                jvm.Home.FullName,
                jvm.Version,
                jvm.LanguageVersion,
                jvm.Vendor,
                jvm.Architecture,
                jvm.OsName,
                jvm.Provider)).ToArray();
            Console.WriteLine(JsonSerializer.Serialize(dtos, JvmJsonContext.Default.JvmDtoArray));
        }
        else
        {
            TableFormatter.Write(jvms, Console.Out);
        }

        return ExitSuccess;
    }

    private static void PrintHelp() => Console.WriteLine("""
        jdkfind — locate installed JDKs

        Usage: jdkfind [options]

        Options:
          -p, --path           Print home directory paths only (wins over --json)
          -j, --json           Print results as JSON
          -l, --latest         Print only the newest match
          -v, --version <n>    Filter by feature version (e.g. 21)
              --vendor <text>  Filter by vendor substring (case-insensitive)
              --arch <text>    Filter by architecture substring (case-insensitive)
          -h, --help           Show this help

        Exit codes: 0 = found, 1 = none found, 2 = usage error
        """);
}
