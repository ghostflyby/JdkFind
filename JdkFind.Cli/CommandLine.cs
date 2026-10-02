using System.Text.Json;

namespace JdkFind.Cli;

internal static class CommandLine
{
    private const int ExitSuccess = 0;
    private const int ExitNotFound = 1;
    private const int ExitUsage = 2;

    internal static int Run(string[] args)
    {
        Options options;
        try
        {
            options = Options.Parse(args);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine("Run 'jdkfind --help' for usage.");
            return ExitUsage;
        }

        if (options.ShowHelp)
        {
            PrintHelp();
            return ExitSuccess;
        }

        var jvms = new List<Jvm>();
        var findOptions = new JdkFindOptions
        {
            // --path only needs homes; probing every installation would be wasted latency.
            ProbeRuntimeProperties = !options.NoProbe && !options.PathsOnly,
        };
        foreach (var jvm in JdkFinder.Locate(findOptions))
        {
            if (options.LanguageVersion is { } version && jvm.LanguageVersion != version)
                continue;
            if (options.JdkOnly && !jvm.HasCompiler)
                continue;
            if (!MatchesVendorFilter(jvm, options.Vendor))
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
                if (newest is null || jvm.Version > newest.Version)
                    newest = jvm;

            jvms = [newest!];
        }

        if (options.PathsOnly)
        {
            foreach (var jvm in jvms)
            {
                Console.Out.Write(jvm.Home.FullName);
                Console.Out.Write(options.Print0 ? '\0' : '\n');
            }
        }
        else if (options.OutputJson)
        {
            var dtos = jvms.Select(jvm => new JvmDto(
                jvm.Home.FullName,
                jvm.Version.Original,
                jvm.LanguageVersion,
                jvm.HasCompiler,
                jvm.KnownVendor.ToString(),
                jvm.VendorDisplayName,
                jvm.RuntimeName,
                jvm.RuntimeVersion,
                jvm.VmName,
                jvm.VmVersion,
                jvm.Vendor,
                jvm.Architecture,
                jvm.OsName,
                jvm.Providers)).ToArray();
            Console.WriteLine(JsonSerializer.Serialize(dtos, JvmJsonContext.Default.JvmDtoArray));
        }
        else
        {
            TableFormatter.Write(jvms, Console.Out);
        }

        return ExitSuccess;
    }

    /// <summary>
    ///     A JVM passes the vendor filter when the text hits any of its vendor surfaces:
    ///     the raw IMPLEMENTOR string, the normalized known-vendor name, or the display
    ///     name. Null text means no filtering.
    /// </summary>
    internal static bool MatchesVendorFilter(Jvm jvm, string? text)
    {
        if (string.IsNullOrEmpty(text))
            return true;

        return (jvm.Vendor?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false) ||
               jvm.KnownVendor.ToString().Contains(text, StringComparison.OrdinalIgnoreCase) ||
               jvm.VendorDisplayName.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    private static void PrintHelp() => Console.WriteLine("""
        jdkfind — locate installed JDKs

        Usage: jdkfind [options]

        Options:
          -p, --path           Print home directory paths only (wins over --json; implies --no-probe)
          -0, --print0         With --path: separate paths with NUL (for xargs -0)
          -j, --json           Print results as JSON
          -l, --latest         Print only the newest match
          -v, --version <n>    Filter by feature version (e.g. 21)
              --vendor <text>  Filter by vendor substring; matches the raw string, the
                               known vendor and the display name (case-insensitive)
              --arch <text>    Filter by architecture substring (case-insensitive)
              --jdk-only       Only installations that ship a compiler (skip runtimes)
              --no-probe       Skip executing each JVM for runtime properties
          -h, --help           Show this help

        Exit codes: 0 = found, 1 = none found, 2 = usage error
        """);
}
