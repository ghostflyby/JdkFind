namespace JdkFind.Providers;

/// <summary>
///     Scans Homebrew OpenJDK kegs (<c>&lt;prefix&gt;/opt/openjdk*</c>) under the prefixes
///     indicated by <c>HOMEBREW_PREFIX</c> and the conventional locations for Apple
///     Silicon, Intel Macs and Linuxbrew. The keg layout resolves through
///     <c>libexec/openjdk.jdk/Contents/Home</c>.
/// </summary>
public sealed class HomebrewJvmProvider(IEnumerable<string> prefixes) : ICommonPrefixesJvmProvider
{
    /// <inheritdoc />
    public string Name => "homebrew";

    private readonly string[] prefixList = [.. prefixes.Where(p => !string.IsNullOrWhiteSpace(p))];

    /// <summary>Scans the conventional Homebrew prefixes and <c>HOMEBREW_PREFIX</c>.</summary>
    public HomebrewJvmProvider() : this(ResolveDefaultPrefixes())
    {
    }

    string ICommonPrefixesJvmProvider.SearchPattern => "openjdk*";

    IEnumerable<string> ICommonPrefixesJvmProvider.GetCommonPrefixes() =>
        prefixList.Select(prefix => Path.Combine(prefix, "opt"));

    private static IEnumerable<string> ResolveDefaultPrefixes()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
            return [];

        var prefixes = new List<string>();
        var env = Environment.GetEnvironmentVariable("HOMEBREW_PREFIX");
        if (!string.IsNullOrWhiteSpace(env))
            prefixes.Add(env);

        prefixes.AddRange(["/opt/homebrew", "/usr/local", "/home/linuxbrew/.linuxbrew"]);
        return prefixes;
    }
}
