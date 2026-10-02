namespace JdkFind.Cli;

internal sealed class Options
{
    internal bool ShowHelp { get; private set; }

    internal bool OutputJson { get; private set; }

    internal bool PathsOnly { get; private set; }

    internal bool Latest { get; private set; }

    internal int? LanguageVersion { get; private set; }

    internal string? Vendor { get; private set; }

    internal string? Architecture { get; private set; }

    internal bool NoProbe { get; private set; }

    internal bool JdkOnly { get; private set; }

    internal bool Print0 { get; private set; }

    internal static Options Parse(string[] args)
    {
        var options = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            var (name, inlineValue) = SplitOption(args[i]);
            switch (name)
            {
                case "-h" or "--help":
                    options.ShowHelp = true;
                    break;

                case "-j" or "--json":
                    options.OutputJson = true;
                    break;

                case "-p" or "--path" or "--paths":
                    options.PathsOnly = true;
                    break;

                case "-l" or "--latest":
                    options.Latest = true;
                    break;

                case "-v" or "--version":
                    options.LanguageVersion = ParseInt(TakeValue(ref i, inlineValue, args, name));
                    break;

                case "--vendor":
                    options.Vendor = TakeValue(ref i, inlineValue, args, name);
                    break;

                case "--arch":
                    options.Architecture = TakeValue(ref i, inlineValue, args, name);
                    break;

                case "--no-probe":
                    options.NoProbe = true;
                    break;

                case "--jdk-only":
                    options.JdkOnly = true;
                    break;

                case "-0" or "--print0":
                    options.Print0 = true;
                    break;

                default:
                    throw new ArgumentException($"Unknown option '{args[i]}'.");
            }
        }

        return options;
    }

    private static (string Name, string? InlineValue) SplitOption(string arg)
    {
        if (!arg.StartsWith('-'))
            throw new ArgumentException($"Unknown argument '{arg}'.");

        var separator = arg.IndexOf('=');
        return separator < 0 ? (arg, null) : (arg[..separator], arg[(separator + 1)..]);
    }

    private static string TakeValue(ref int i, string? inlineValue, string[] args, string name)
    {
        if (inlineValue is not null)
            return inlineValue;

        return i + 1 >= args.Length ? throw new ArgumentException($"Missing value for '{name}'.") : args[++i];
    }

    private static int ParseInt(string value) =>
        int.TryParse(value, out var parsed) && parsed >= 0
            ? parsed
            : throw new ArgumentException($"Invalid version '{value}'.");
}
