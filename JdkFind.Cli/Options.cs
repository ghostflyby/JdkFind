namespace JdkFind.Cli;

internal enum SubCommand
{
    None,
    Info,
    List,
}

internal sealed class Options
{
    internal SubCommand Command { get; private set; }

    /// <summary>Positional version prefix (numeric, dot-separated, e.g. "21", "21.0", "21.0.5").</summary>
    internal string? VersionPrefix { get; private set; }

    /// <summary>Positional bin tool name (non-numeric, e.g. "java", "javac") for the default command.</summary>
    internal string? Tool { get; private set; }

    internal bool ShowHelp { get; private set; }

    internal bool OutputJson { get; private set; }

    internal string? Vendor { get; private set; }

    internal string? Distribution { get; private set; }

    internal string? Architecture { get; private set; }

    internal int? Release { get; private set; }

    internal bool JdkOnly { get; private set; }

    internal bool NoProbe { get; private set; }

    internal static Options Parse(string[] args)
    {
        var options = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith('-'))
            {
                ParsePositional(options, arg);
                continue;
            }

            var (name, inlineValue) = SplitOption(arg);
            switch (name)
            {
                case "-h" or "--help":
                    options.ShowHelp = true;
                    break;

                case "-j" or "--json":
                    options.OutputJson = true;
                    break;

                case "--vendor":
                    options.Vendor = TakeValue(ref i, inlineValue, args, name);
                    break;

                case "--distribution":
                    options.Distribution = TakeValue(ref i, inlineValue, args, name);
                    break;

                case "--arch":
                    options.Architecture = TakeValue(ref i, inlineValue, args, name);
                    break;

                case "--release":
                    options.Release = int.Parse(
                        ValidateVersionPrefix(TakeValue(ref i, inlineValue, args, name)),
                        System.Globalization.CultureInfo.InvariantCulture);
                    break;

                case "--jdk-only":
                    options.JdkOnly = true;
                    break;

                case "--no-probe":
                    options.NoProbe = true;
                    break;

                default:
                    throw new ArgumentException($"Unknown option '{arg}'.");
            }
        }

        return options;
    }

    private static void ParsePositional(Options options, string arg)
    {
        switch (arg)
        {
            case "info":
                if (options.Command != SubCommand.None)
                    throw new ArgumentException($"Unexpected argument '{arg}'.");
                options.Command = SubCommand.Info;
                return;

            case "list":
                if (options.Command != SubCommand.None)
                    throw new ArgumentException($"Unexpected argument '{arg}'.");
                options.Command = SubCommand.List;
                return;
        }

        // A leading digit makes the positional a version prefix; anything else is a
        // bin tool name for the default command.
        if (char.IsAsciiDigit(arg[0]))
        {
            if (options.VersionPrefix is not null)
                throw new ArgumentException($"Unexpected argument '{arg}'.");

            options.VersionPrefix = ValidateVersionPrefix(arg);
            return;
        }

        if (options.Command != SubCommand.None || options.Tool is not null)
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

    private static (string Name, string? InlineValue) SplitOption(string arg)
    {
        var separator = arg.IndexOf('=');
        return separator < 0 ? (arg, null) : (arg[..separator], arg[(separator + 1)..]);
    }

    private static string TakeValue(ref int i, string? inlineValue, string[] args, string name)
    {
        if (inlineValue is not null)
            return inlineValue;

        return i + 1 >= args.Length ? throw new ArgumentException($"Missing value for '{name}'.") : args[++i];
    }
}
