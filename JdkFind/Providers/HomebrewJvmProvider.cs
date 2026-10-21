namespace JdkFind.Providers;

/// <summary>
///     Scans the Homebrew kegs that ship a JDK under the prefixes indicated by
///     <c>HOMEBREW_PREFIX</c> and the conventional locations for Apple Silicon,
///     Intel Macs and Linuxbrew. Only the known JDK formulae are validated — the
///     keg names come from <see cref="IsKnownJdkKeg" />, so a keg that is not one
///     never reaches the layout probe, which would otherwise stat its way through
///     every keg on the machine. The keg layout resolves through <c>libexec</c>:
///     the macOS <c>libexec/&lt;name&gt;.jdk/Contents/Home</c> bundle or, on Linux, the
///     image installed straight into <c>libexec</c>. Vendors that publish their JDK
///     as a cask rather than a formula (Zulu, Corretto, Temurin, Microsoft, Oracle,
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

    string? ICommonPrefixesJvmProvider.GetJavaHome(string subDirectory) =>
        IsKnownJdkKeg(Path.GetFileName(subDirectory)) ? JavaHomeLayout.Probe(subDirectory) : null;

    /// <summary>
    ///     True for the keg names homebrew-core publishes a JDK under: <c>openjdk</c>
    ///     with its versioned flavours (<c>openjdk@8/11/17/21/25</c>), the <c>java</c>
    ///     alias that formula carries, and <c>graalvm</c>. Everything else offering a
    ///     JDK through Homebrew is a cask, which the macOS source covers; a keg from
    ///     a third-party tap is not scanned either, since its name is not known here.
    /// </summary>
    private static bool IsKnownJdkKeg(string kegName) =>
        kegName.StartsWith("openjdk", StringComparison.OrdinalIgnoreCase) ||
        kegName.Equals("java", StringComparison.OrdinalIgnoreCase) ||
        kegName.Equals("graalvm", StringComparison.OrdinalIgnoreCase);

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
