using System.CommandLine;
using System.CommandLine.Help;
using System.Globalization;

namespace JdkFind.Cli;

internal enum SubCommand
{
    None,
    Info,
    List,
}

    /// <summary>
    ///     The parsed jdkfind command line. The options and the info/list subcommands
    ///     are declared on System.CommandLine's root command; Parse drives the
    ///     framework parser and maps its result onto this type. Classification of the
    ///     default command's positionals (version prefix vs. bin tool) stays here,
    ///     because the framework cannot express positionals whose meaning depends on
    ///     the first character.
    /// </summary>
    internal sealed class Options
{
    /// <summary>Prose carried at the top of the generated help: usage lines,
    /// selection semantics, stream discipline and exit codes.</summary>
    private const string RootDescription = """
        jdkfind [version] [tool] [options]    Print the selected home, or bin/<tool> path
        jdkfind info [version] [options]      Print one installation's details
        jdkfind list [options]                List all matching installations

        The version is a numeric prefix (21, 21.0, 21.0.5); the selection is stable —
        newest version first, installations with a compiler preferred on ties.

        Streams: machine-readable output goes to stdout; the human-readable list and
        details go to stderr.

        Exit codes: 0 = found, 1 = none found, 2 = usage error
        """;

    private readonly ParseResult parseResult;
    private readonly VersionOption versionOption;

    private Options(ParseResult parseResult, VersionOption versionOption)
    {
        this.parseResult = parseResult;
        this.versionOption = versionOption;
    }

    /// <summary>True when the framework's help action was requested (-h/--help).</summary>
    internal bool ShowHelp => parseResult.Action is HelpAction;

    /// <summary>True when the framework's version action was requested (--version).</summary>
    internal bool ShowVersion => parseResult.GetResult(versionOption) is not null;

    internal SubCommand Command { get; private set; }

    /// <summary>Positional version prefix (numeric, dot-separated, e.g. "21", "21.0", "21.0.5").</summary>
    internal string? VersionPrefix { get; private set; }

    /// <summary>Positional bin tool name (non-numeric, e.g. "java", "javac") for the default command.</summary>
    internal string? Tool { get; private set; }

    internal bool OutputJson { get; private set; }

    internal string? Vendor { get; private set; }

    internal string? Distribution { get; private set; }

    internal string? Architecture { get; private set; }

    internal int? Release { get; private set; }

    internal bool JdkOnly { get; private set; }

    internal bool NoProbe { get; private set; }

    /// <summary>Parses the arguments through System.CommandLine; the first parse
    /// error surfaces as an ArgumentException, matching the previous parser's seam.</summary>
    internal static Options Parse(string[] args)
    {
        var operands = new Argument<string[]>("operands")
        {
            Description = "a digit-led value is a version prefix (21, 21.0.5); " +
                          "anything else is a bin tool name (java, javac)",
        };

        var version = new Argument<string?>("version")
        {
            Description = "Numeric version prefix (21, 21.0.5)",
            Arity = ArgumentArity.ZeroOrOne,
        };

        var json = new Option<bool>("--json", "-j")
        {
            Description = "Write JSON to stdout (list: array; others: single object)",
            Recursive = true,
        };

        var vendor = new Option<string>("--vendor")
        {
            Description = "Filter by vendor substring; matches the normalized vendor and the raw string (case-insensitive)",
            Recursive = true,
        };

        var distribution = new Option<string>("--distribution")
        {
            Description = "Filter by distribution substring per the foojay API names (e.g. temurin, zulu, corretto)",
            Recursive = true,
        };

        var architecture = new Option<string>("--arch")
        {
            Description = "Filter by architecture substring (case-insensitive); known aliases match too (x86_64/amd64/x64, aarch64/arm64)",
            Recursive = true,
        };

        var release = new Option<string>("--release")
        {
            Description = "Filter by supported javac language level; covers --release/-source/-target compilation",
            Recursive = true,
        };

        var jdkOnly = new Option<bool>("--jdk-only")
        {
            Description = "Only installations that ship a compiler (skip runtimes)",
            Recursive = true,
        };

        var noProbe = new Option<bool>("--no-probe")
        {
            Description = "Skip executing each JVM for runtime properties",
            Recursive = true,
        };

        var info = new Command("info")
        {
            Description = "Print one installation's details",
            // A tool name after info is a usage error, not an ignored token.
            TreatUnmatchedTokensAsErrors = true,
        };
        info.Add(version);

        var list = new Command("list")
        {
            Description = "List all matching installations",
            TreatUnmatchedTokensAsErrors = true,
        };

        var root = new RootCommand(RootDescription)
        {
            // Tokens matched by nothing on the default command are usage errors too.
            TreatUnmatchedTokensAsErrors = true,
        };
        root.Add(operands);
        root.Add(json);
        root.Add(vendor);
        root.Add(distribution);
        root.Add(architecture);
        root.Add(release);
        root.Add(jdkOnly);
        root.Add(noProbe);
        root.Add(info);
        root.Add(list);

        // The root doubles as the default command (bare `jdkfind` locates and
        // prints); with subcommands present the framework would otherwise demand
        // one of them. The action is never invoked — Run owns the dispatch — this
        // only marks the root callable.
        root.SetAction(_ => 0);

        // The root command auto-injects a standard --version option; keep it and
        // let Run dispatch it like the help action.
        var versionOption = (VersionOption)root.Options.Single(option => option is VersionOption);

        var parseResult = root.Parse(args, new ParserConfiguration { EnablePosixBundling = false });
        if (parseResult.Errors.Count > 0)
            throw new ArgumentException(parseResult.Errors[0].Message);

        var infoVersion = parseResult.GetValue(version);
        var options = new Options(parseResult, versionOption)
        {
            VersionPrefix = infoVersion is null ? null : ValidateVersionPrefix(infoVersion),
            OutputJson = parseResult.GetValue(json),
            Vendor = parseResult.GetValue(vendor),
            Distribution = parseResult.GetValue(distribution),
            Architecture = parseResult.GetValue(architecture),
            Release = parseResult.GetValue(release) is { } text ? ParseRelease(text) : null,
            JdkOnly = parseResult.GetValue(jdkOnly),
            NoProbe = parseResult.GetValue(noProbe),
        };

        foreach (var operand in parseResult.GetValue(operands) ?? [])
            ParsePositional(options, operand);

        // Mapped after the operand classification so legacy orderings like
        // `jdkfind java list` keep the same result.
        options.Command = parseResult.CommandResult.Command == info ? SubCommand.Info
            : parseResult.CommandResult.Command == list ? SubCommand.List
            : SubCommand.None;

        return options;
    }

    /// <summary>Prints the framework-generated help to stdout; only meaningful
    /// when ShowHelp is true.</summary>
    internal int RenderHelp()
    {
        // The framework labels the usage line after the entry assembly (the test
        // host under tests, JdkFind.Cli for the shipped tool); jdkfind's grammar
        // is always invoked as jdkfind, so patch the label before it reaches stdout.
        var buffer = new StringWriter();
        var exit = parseResult.Invoke(new InvocationConfiguration { Output = buffer });
        Console.Out.Write(buffer.ToString().Replace("JdkFind.Cli", "jdkfind"));
        return exit;
    }

    /// <summary>Prints the framework's version line to stdout; only meaningful
    /// when ShowVersion is true.</summary>
    internal int RenderVersion() => parseResult.Invoke(new InvocationConfiguration());

    private static void ParsePositional(Options options, string arg)
    {
        if (arg.Length == 0)
            throw new ArgumentException("Unexpected argument ''.");

        // The framework routes option-looking tokens it cannot match into the
        // operand collection; jdkfind's grammar keeps rejecting them as unknown
        // options (exit 2) rather than reading them as tool names.
        if (arg.StartsWith('-'))
            throw new ArgumentException($"Unknown option '{arg}'.");

        // A leading digit makes the positional a version prefix; anything else is a
        // bin tool name for the default command.
        if (char.IsAsciiDigit(arg[0]))
        {
            if (options.VersionPrefix is not null)
                throw new ArgumentException($"Unexpected argument '{arg}'.");

            options.VersionPrefix = ValidateVersionPrefix(arg);
            return;
        }

        if (options.Tool is not null)
            throw new ArgumentException($"Unexpected argument '{arg}'.");

        options.Tool = arg.Contains('/') || arg.Contains('\\') || arg.Contains('=')
            ? throw new ArgumentException($"Invalid tool name '{arg}'.")
            : arg;
    }

    private static string ValidateVersionPrefix(string arg)
    {
        var segments = arg.Split('.');
        if (segments.Length is < 1 or > 3 || segments.Any(s => s.Length == 0 || !s.All(char.IsAsciiDigit)))
            throw new ArgumentException($"Invalid version prefix '{arg}'.");

        return arg;
    }

    /// <summary>The release filter is a single integer language level — unlike the
    /// version prefix, which may carry up to three dot-separated segments.</summary>
    private static int ParseRelease(string text) =>
        text.Length > 0 && text.All(char.IsAsciiDigit)
            ? int.Parse(text, CultureInfo.InvariantCulture)
            : throw new ArgumentException($"Invalid release '{text}'.");
}
