namespace JdkFind.Providers;

/// <summary>
///     Scans Homebrew kegs under the prefixes indicated by <c>HOMEBREW_PREFIX</c>
///     and the conventional locations for Apple Silicon, Intel Macs and Linuxbrew.
///     The keg layout resolves through <c>libexec</c>: the macOS
///     <c>libexec/&lt;name&gt;.jdk/Contents/Home</c> bundle — <c>openjdk</c> and
///     <c>graalvm</c> are the formulae that ship one — or, on Linux, the image
///     installed straight into <c>libexec</c>. Vendors that publish their JDK as a
///     cask rather than a formula (Zulu, Corretto, Temurin, Microsoft, Oracle,
///     SAP, Semeru) install into <c>/Library/Java/JavaVirtualMachines</c> and are
///     found by the macOS source instead.
/// </summary>
public sealed class HomebrewJvmProvider(IEnumerable<string> prefixes) : ICommonPrefixesJvmProvider
{
    /// <inheritdoc />
    public string Name => "homebrew";

    private readonly string[] _prefixList = [.. prefixes.Where(p => !string.IsNullOrWhiteSpace(p))];

    /// <summary>Scans the conventional Homebrew prefixes and <c>HOMEBREW_PREFIX</c>.</summary>
    public HomebrewJvmProvider() : this(ResolveDefaultPrefixes()) { }

    IEnumerable<string> ICommonPrefixesJvmProvider.GetCommonPrefixes() =>
        _prefixList.Select(prefix => Path.Combine(prefix, "opt"));

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
