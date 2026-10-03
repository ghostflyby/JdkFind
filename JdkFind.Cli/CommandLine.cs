using System.CommandLine;
using System.Globalization;
using System.Text.Json;

namespace JdkFind.Cli;

internal static class CommandLine
{
    private const int ExitSuccess = 0;
    private const int ExitNotFound = 1;
    private const int ExitUsage = 2;

    /// <summary>POSIX convention for a run ended by SIGINT (128 + 2).</summary>
    private const int ExitCancelled = 130;

    /// <summary>Selection among matching installations: newest version first, then
    /// compiler-carrying installations preferred — stable per machine state.</summary>
    private static readonly Comparer<Jvm> SelectionOrder = Comparer<Jvm>.Create((a, b) =>
    {
        var version = a.Version.CompareTo(b.Version);
        return version != 0 ? version : a.HasCompiler.CompareTo(b.HasCompiler);
    });

    internal static int Run(string[] args) => Run(args, findOptions: null);

    /// <summary>Injection seam for tests: pass explicit locate options to run against
    /// controlled fixtures instead of the real machine.</summary>
    internal static int Run(string[] args, JdkFindOptions? findOptions) =>
        RunAsync(args, findOptions).GetAwaiter().GetResult();

    /// <summary>
    ///     Async entry point. Dispatch goes through System.CommandLine's invocation so
    ///     the framework's termination-signal token (Ctrl+C, SIGINT, SIGTERM) reaches
    ///     the actions — the locate pipeline (runtime probes, release-file reads)
    ///     honors it.
    /// </summary>
    internal static async Task<int> RunAsync(string[] args, JdkFindOptions? findOptions = null)
    {
        var tree = Options.CreateTree((options, cancellationToken) =>
            ExecuteAsync(options, cancellationToken, findOptions));
        var parseResult = tree.Root.Parse(args, new ParserConfiguration { EnablePosixBundling = false });

        if (parseResult.Errors.Count > 0)
        {
            await Console.Error.WriteLineAsync(parseResult.Errors[0].Message);
            await Console.Error.WriteLineAsync("Run 'jdkfind --help' for usage.");
            return ExitUsage;
        }

        Options options;
        try
        {
            options = Options.Map(parseResult, tree);
        }
        catch (ArgumentException exception)
        {
            await Console.Error.WriteLineAsync(exception.Message);
            await Console.Error.WriteLineAsync("Run 'jdkfind --help' for usage.");
            return ExitUsage;
        }

        if (options.ShowHelp)
            return options.RenderHelp();

        if (options.ShowVersion)
            return options.RenderVersion();

        return await parseResult.InvokeAsync(new InvocationConfiguration()).ConfigureAwait(false);
    }

    /// <summary>The action shared by the default command and both subcommands:
    /// runs the locate pipeline and prints the selected installation(s).</summary>
    private static async Task<int> ExecuteAsync(
        Options options, CancellationToken cancellationToken, JdkFindOptions? findOptions)
    {
        var locateOptions = findOptions ?? new JdkFindOptions { ProbeRuntimeProperties = !options.NoProbe };

        IReadOnlyList<Jvm> located;
        try
        {
            located = await JdkFinder.LocateAsync(locateOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancelled from a termination signal: both streams stay empty.
            return ExitCancelled;
        }

        var matches = located.Where(jvm => MatchesFilters(jvm, options)).ToList();

        if (matches.Count == 0)
            return ExitNotFound;

        // --json wins over the human formats on every command.
        if (options.OutputJson)
        {
            if (options.Command == SubCommand.List)
            {
                Console.WriteLine(JsonSerializer.Serialize(
                    [.. matches.Select(ToDto)], JvmJsonContext.Default.JvmDtoArray));
            }
            else
            {
                var selected = matches.OrderByDescending(jvm => jvm, SelectionOrder).First();
                Console.WriteLine(JsonSerializer.Serialize(ToDto(selected), JvmJsonContext.Default.JvmDto));
            }

            return ExitSuccess;
        }

        if (options.Command == SubCommand.List)
        {
            TableFormatter.Write(matches, Console.Error);
            return ExitSuccess;
        }

        // Every other command selects exactly one installation: newest version first,
        // then compiler-carrying preferred, then first-discovered. Same machine state
        // always produces the same selection.
        var chosen = matches.OrderByDescending(jvm => jvm, SelectionOrder).First();

        if (options.Command == SubCommand.Info)
        {
            WriteInfo(chosen, Console.Error);
            return ExitSuccess;
        }

        await Console.Out.WriteLineAsync(options.Tool is null
            ? chosen.Home.FullName
            : ToolPath(chosen, options.Tool));
        return ExitSuccess;
    }

    internal static string ToolPath(Jvm jvm, string? tool)
    {
        if (string.IsNullOrEmpty(tool))
            return jvm.Home.FullName;

        var fileName = OperatingSystem.IsWindows() ? tool + ".exe" : tool;
        return Path.Combine(jvm.Home.FullName, "bin", fileName);
    }

    /// <summary>A JVM matches the version prefix when its core version starts with it segment-wise.</summary>
    internal static bool MatchesVersion(Jvm jvm, string versionPrefix)
    {
        var segments = versionPrefix.Split('.');
        var core = jvm.Version.Core;
        for (var index = 0; index < segments.Length; index++)
            if (Segment(core, index) != int.Parse(segments[index], CultureInfo.InvariantCulture))
                return false;

        return true;
    }

    private static int Segment(Version version, int index) => index switch
    {
        0 => version.Major,
        1 => version.Minor,
        2 => Math.Max(version.Build, 0),
        _ => Math.Max(version.Revision, 0),
    };

    private static bool MatchesFilters(Jvm jvm, Options options) =>
        (options.VersionPrefix is null || MatchesVersion(jvm, options.VersionPrefix)) &&
        (options.Release is null || jvm.SupportsSource(options.Release.Value)) &&
        (!options.JdkOnly || jvm.HasCompiler) &&
        MatchesVendorFilter(jvm, options.Vendor) &&
        MatchesDistributionFilter(jvm, options.Distribution) &&
        MatchesArchFilter(jvm, options.Architecture);

    /// <summary>
    ///     A JVM passes the vendor filter when the text hits the normalized vendor name
    ///     or the raw IMPLEMENTOR string. Null text means no filtering.
    /// </summary>
    internal static bool MatchesVendorFilter(Jvm jvm, string? text)
    {
        if (string.IsNullOrEmpty(text))
            return true;

        return (jvm.VendorRaw?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false) ||
               jvm.Vendor.ToString().Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A JVM passes the distribution filter when the foojay-style name contains the text.</summary>
    internal static bool MatchesDistributionFilter(Jvm jvm, string? text)
    {
        if (string.IsNullOrEmpty(text))
            return true;

        return jvm.Distribution.ToString().Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Architecture aliases whose spellings release files disagree on; a filter
    ///     text naming one member matches installations spelled as any other.
    /// </summary>
    private static readonly string[][] ArchAliases =
    [
        ["x86_64", "amd64", "x64"],
        ["aarch64", "arm64"]
    ];

    /// <summary>
    ///     A JVM passes the architecture filter when the raw OS_ARCH / os.arch value
    ///     contains the text (case-insensitive). A text naming one well-known alias
    ///     (x86_64/amd64/x64, aarch64/arm64) matches every spelling in that group;
    ///     any other text is an ordinary substring match.
    /// </summary>
    internal static bool MatchesArchFilter(Jvm jvm, string? text)
    {
        if (string.IsNullOrEmpty(text))
            return true;

        var architecture = jvm.Architecture;
        if (string.IsNullOrEmpty(architecture))
            return false;

        return ArchAliases.FirstOrDefault(group => group.Contains(text, StringComparer.OrdinalIgnoreCase)) is { } group
            ? group.Any(alias => architecture.Contains(alias, StringComparison.OrdinalIgnoreCase))
            : architecture.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    private static JvmDto ToDto(Jvm jvm) => new(
        jvm.Home.FullName,
        jvm.Version.Original,
        jvm.LanguageVersion,
        jvm.HasCompiler,
        jvm.Version.IsPreRelease,
        jvm.Vendor.ToString(),
        jvm.Distribution.ToString(),
        jvm.VendorRaw,
        jvm.RuntimeName,
        jvm.RuntimeVersion,
        jvm.VmName,
        jvm.VmVersion,
        jvm.Architecture,
        jvm.OsName,
        jvm.Providers);

    internal static void WriteInfo(Jvm jvm, TextWriter writer)
    {
        writer.WriteLine($"home: {jvm.Home.FullName}");
        writer.WriteLine($"version: {jvm.Version.Original} (feature {jvm.LanguageVersion?.ToString() ?? "unknown"})");
        writer.WriteLine($"vendor: {jvm.Vendor}");
        writer.WriteLine($"distribution: {jvm.Distribution}");

        if (!string.IsNullOrEmpty(jvm.RuntimeName))
            writer.WriteLine($"runtime: {jvm.RuntimeName} {jvm.RuntimeVersion}");
        if (!string.IsNullOrEmpty(jvm.VmName))
            writer.WriteLine($"vm: {jvm.VmName} {jvm.VmVersion}");
        if (!string.IsNullOrEmpty(jvm.Architecture))
            writer.WriteLine($"arch: {jvm.Architecture}");

        writer.WriteLine($"type: {(jvm.HasCompiler ? "jdk" : "jre")}");
        writer.WriteLine($"providers: {string.Join(", ", jvm.Providers)}");
    }
}