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
        jdkfind [version] -- <command>        Run <command> with JAVA_HOME and bin/ of the selection (exec on Unix, spawn on Windows)
        jdkfind info [version] [options]      Print one installation's details
        jdkfind list [options]                List all matching installations

        The version is a numeric prefix (21, 21.0, 21.0.5); the selection is stable —
        newest version first, installations with a compiler preferred on ties.

        Streams: machine-readable output goes to stdout; the human-readable list and
        details go to stderr.

        Exit codes: 0 = found, 1 = none found, 2 = usage error, 130 = cancelled;
        run mode adds 126/127 when the command cannot be executed or is not found
        """;

    private readonly ParseResult parseResult;
    private readonly CommandTree tree;
    private readonly VersionOption versionOption;

    private Options(ParseResult parseResult, CommandTree tree, VersionOption versionOption)
    {
        this.parseResult = parseResult;
        this.tree = tree;
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

    /// <summary>The command and arguments given after '--', or null when no '--' was present.</summary>
    internal IReadOnlyList<string>? CommandArgs { get; private set; }

    internal bool OutputJson { get; private set; }

    internal string? Vendor { get; private set; }

    internal string? Distribution { get; private set; }

    internal string? Architecture { get; private set; }

    internal int? Release { get; private set; }

    internal bool JdkOnly { get; private set; }

    internal bool NoProbe { get; private set; }

    /// <summary>Parses the arguments through System.CommandLine — the '--' tail
    /// lands on <see cref="CommandArgs" /> — surfacing the first parse error as an
    /// ArgumentException. The execution is bound later, by
    /// <see cref="InvokeAsync" />.</summary>
    internal static Options Parse(string[] args)
    {
        var tree = CreateTree();

        // Everything after '--' never reaches the parser: it is the verbatim
        // command for run mode.
        var separator = Array.IndexOf(args, "--");
        var tail = separator < 0 ? null : args[(separator + 1)..];
        var head = separator < 0 ? args : args[..separator];

        var parseResult = tree.Root.Parse(head, new ParserConfiguration { EnablePosixBundling = false });
        if (parseResult.Errors.Count > 0)
            throw new ArgumentException(parseResult.Errors[0].Message);

        // A help token clears subcommand-level parse errors, but unmatched tokens
        // survive it; keep them a usage error (`list --bogus --help` must not print
        // help with exit 0).
        if (parseResult.UnmatchedTokens is { Count: > 0 })
            throw new ArgumentException(parseResult.UnmatchedTokens[0].StartsWith('-')
                ? $"Unknown option '{parseResult.UnmatchedTokens[0]}'."
                : $"Unexpected argument '{parseResult.UnmatchedTokens[0]}'.");

        var options = Map(parseResult, tree);

        if (tail is not null)
        {
            if (options.Command != SubCommand.None)
                throw new ArgumentException("'--' runs a command; it is not valid with 'info' or 'list'.");
            if (options.Tool is not null)
                throw new ArgumentException("'--' runs a command; the tool positional has no effect with it.");
            if (tail.Length == 0)
                throw new ArgumentException("No command given after '--'.");
            options.CommandArgs = tail;
        }

        return options;
    }

    /// <summary>Assembles the parse-only command tree. A placeholder root action
    /// marks the default command callable for bare `jdkfind`; the real execution
    /// is bound by <see cref="InvokeAsync" /> after parsing.</summary>
    internal static CommandTree CreateTree()
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

        var listVersion = new Argument<string?>("version")
        {
            Description = "Numeric version prefix (21, 21.0.5)",
            Arity = ArgumentArity.ZeroOrOne,
        };
        list.Add(listVersion);

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

        // The root command auto-injects a standard --version option; keep it and
        // let Run dispatch it like the help action.
        var versionOption = (VersionOption)root.Options.Single(option => option is VersionOption);

        var tree = new CommandTree
        {
            Root = root,
            Info = info,
            List = list,
            Operands = operands,
            Version = version,
            ListVersion = listVersion,
            Json = json,
            Vendor = vendor,
            Distribution = distribution,
            Architecture = architecture,
            Release = release,
            JdkOnly = jdkOnly,
            NoProbe = noProbe,
            VersionOption = versionOption,
        };

        // Parse-only tree: with subcommands present the framework would demand
        // one of them; the placeholder just marks the root (the default command)
        // callable for bare `jdkfind`. InvokeAsync binds the real execution.
        root.SetAction(_ => 0);

        return tree;
    }

    /// <summary>Maps a successful parse result onto an Options instance.</summary>
    internal static Options Map(ParseResult parseResult, CommandTree tree)
    {
        var infoVersion = parseResult.GetValue(tree.Version);
        var listVersion = parseResult.GetValue(tree.ListVersion);
        var options = new Options(parseResult, tree, tree.VersionOption)
        {
            VersionPrefix = infoVersion is not null ? ValidateVersionPrefix(infoVersion)
                : listVersion is not null ? ValidateVersionPrefix(listVersion)
                : null,
            OutputJson = parseResult.GetValue(tree.Json),
            Vendor = parseResult.GetValue(tree.Vendor),
            Distribution = parseResult.GetValue(tree.Distribution),
            Architecture = parseResult.GetValue(tree.Architecture),
            Release = parseResult.GetValue(tree.Release) is { } text ? ParseRelease(text) : null,
            JdkOnly = parseResult.GetValue(tree.JdkOnly),
            NoProbe = parseResult.GetValue(tree.NoProbe),
        };

        foreach (var operand in parseResult.GetValue(tree.Operands) ?? [])
            ParsePositional(options, operand);

        // Mapped after the operand classification so legacy orderings like
        // `jdkfind java list` keep the same result.
        options.Command = parseResult.CommandResult.Command == tree.Info ? SubCommand.Info
            : parseResult.CommandResult.Command == tree.List ? SubCommand.List
            : SubCommand.None;

        return options;
    }

    /// <summary>
    ///     The single parsing path for both the test seam and the wired invocation
    ///     tree, so '--' handling cannot diverge between them: everything after '--'
    ///     becomes <see cref="CommandArgs" /> verbatim (it never reaches the parser),
    ///     and the first parse, grammar or unmatched-token error surfaces as an
    ///     ArgumentException. The command is also assigned to
    ///     <paramref name="commandArgs" /> for the invocation closure, because the
    ///     wired action re-maps the parse result into a fresh Options instance.
    /// </summary>
    internal static Options ParseInto(CommandTree tree, string[] args, out string[]? commandArgs)
    {
        var separator = Array.IndexOf(args, "--");
        var tail = separator < 0 ? null : args[(separator + 1)..];
        var head = separator < 0 ? args : args[..separator];
        commandArgs = tail;

        var parseResult = tree.Root.Parse(head, new ParserConfiguration { EnablePosixBundling = false });
        if (parseResult.Errors.Count > 0)
            throw new ArgumentException(parseResult.Errors[0].Message);

        // A help token clears subcommand-level parse errors, but unmatched tokens
        // survive it; keep them a usage error (`list --bogus --help` must not print
        // help with exit 0).
        if (parseResult.UnmatchedTokens is { Count: > 0 })
            throw new ArgumentException(parseResult.UnmatchedTokens[0].StartsWith('-')
                ? $"Unknown option '{parseResult.UnmatchedTokens[0]}'."
                : $"Unexpected argument '{parseResult.UnmatchedTokens[0]}'.");

        var options = Map(parseResult, tree);

        if (tail is not null)
        {
            if (options.Command != SubCommand.None)
                throw new ArgumentException("'--' runs a command; it is not valid with 'info' or 'list'.");
            if (options.Tool is not null)
                throw new ArgumentException("'--' runs a command; the tool positional has no effect with it.");
            if (tail.Length == 0)
                throw new ArgumentException("No command given after '--'.");
            options.CommandArgs = tail;
        }

        return options;
    }

    /// <summary>Prints the framework-generated help or version output to stdout;
    /// only meaningful when ShowHelp or ShowVersion is true.</summary>
    internal int RenderFrameworkOutput() => parseResult.Invoke(new InvocationConfiguration());

    /// <summary>
    ///     Binds the run execution to the parsed commands — the framework's
    ///     termination-signal cancellation token reaches it — and invokes.
    ///     Binding happens after parsing, so the execution receives the fully
    ///     parsed <see cref="Options" /> (including <see cref="CommandArgs" />)
    ///     instead of closing over pre-parse state.
    /// </summary>
    internal Task<int> InvokeAsync(Func<CancellationToken, Task<int>> execute)
    {
        Task<int> action(ParseResult _, CancellationToken cancellationToken) => execute(cancellationToken);

        tree.Root.SetAction(action);
        tree.Info.SetAction(action);
        tree.List.SetAction(action);

        return parseResult.InvokeAsync(new InvocationConfiguration());
    }

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
        if (segments.Any(s => !int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out _)))
            throw new ArgumentException($"Invalid version prefix '{arg}'.");

        return arg;
    }

    /// <summary>The release filter is a single integer language level — unlike the
    /// version prefix, which may carry up to three dot-separated segments.</summary>
    private static int ParseRelease(string text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var release)
            ? release
            : throw new ArgumentException($"Invalid release '{text}'.");
}
