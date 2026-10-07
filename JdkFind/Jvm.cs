namespace JdkFind;

/// <summary>
///     A located JVM installation: a java home directory and what its release
///     file says about it. Running a java program needs only the binary —
///     <see cref="Executable" /> — while the installation owns the layout
///     questions (the compiler, other executables via <see cref="Resolve" />).
/// </summary>
public sealed record Jvm
{
    /// <summary>The Java home directory.</summary>
    public required DirectoryInfo Home { get; init; }

    /// <summary>Identifiers of every provider that reported this home, in provider order.</summary>
    public required IReadOnlyList<string> Providers { get; init; }

    /// <summary>The home's own java binary — probed when runtime probing ran.</summary>
    public required JavaExecutable Executable { get; init; }

    /// <summary>The JDK version — a comparable value that also carries the raw <c>JAVA_VERSION</c> string.</summary>
    public required JvmVersion Version { get; init; }

    /// <summary>The feature version extracted from <c>JAVA_VERSION</c>; null when it is unparseable.</summary>
    public int? LanguageVersion { get; init; }

    /// <summary>
    ///     Whether this installation's javac accepts language level
    ///     <paramref name="sourceLevel" /> via <c>--release</c> / <c>-source</c> /
    ///     <c>-target</c>. Derived from the major version — javac N supports every
    ///     level up to N, so no separate extraction is needed. Floors below 8
    ///     (dropped across older javac releases) are not modeled; the filter
    ///     horizon is 8+.
    /// </summary>
    public bool SupportsSource(int sourceLevel) => (LanguageVersion ?? 0) >= sourceLevel;

    /// <summary>
    ///     Resolves an executable of this installation by name — <c>java</c>,
    ///     <c>javac</c>, <c>keytool</c>, ... — searching <c>bin</c> and, for the
    ///     JDK 8 inner JRE layout, <c>jre/bin</c>. On Windows the <c>.exe</c>
    ///     suffix may be omitted. Returns null when the installation ships no
    ///     such executable; <paramref name="name" /> must be a bare file name,
    ///     not a path.
    /// </summary>
    public FileInfo? Resolve(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Contains('/') || name.Contains('\\') || Path.IsPathRooted(name))
            throw new ArgumentException("The executable name must be a bare file name, not a path.", nameof(name));

        return JavaHomeLayout.FindExecutable(Home.FullName, name) is { } path ? new FileInfo(path) : null;
    }

    /// <summary>
    ///     True when the installation ships a compiler (<c>bin/javac</c>), i.e. it is a
    ///     JDK rather than a runtime-only image (standalone JREs, jlink runtimes).
    /// </summary>
    public bool HasCompiler { get; init; }

    /// <summary>The release file's raw <c>IMPLEMENTOR</c> string; null when absent.</summary>
    public string? VendorRaw { get; init; }

    /// <summary>The normalized upstream vendor the raw string matched, or <see cref="JvmVendor.Unknown" />.</summary>
    public JvmVendor Vendor { get; init; }

    /// <summary>The distribution per the foojay API naming, or <see cref="JvmDistribution.Unknown" />.</summary>
    public JvmDistribution Distribution { get; init; }

    /// <summary>Summarizes the installation: language version, full version, distribution and home.</summary>
    public override string ToString() =>
        $"{LanguageVersion?.ToString() ?? "?"} ({Version}) {Distribution} — {Home.FullName}";

    /// <summary>Builds the installation a release file describes, with
    /// <paramref name="executable" /> as its own java binary.</summary>
    internal static Jvm FromRelease(string homePath, JavaExecutable executable, IReadOnlyDictionary<string, string> release)
    {
        var version = JvmVersion.Parse(release.GetValueOrDefault("JAVA_VERSION"));
        var vendorRaw = NonEmpty(release.GetValueOrDefault("IMPLEMENTOR"));

        return new Jvm
        {
            Home = new DirectoryInfo(homePath),
            Providers = [],
            Executable = executable,
            Version = version,
            LanguageVersion = ReleaseFile.TryGetLanguageVersion(version.Original),
            HasCompiler = File.Exists(Path.Combine(homePath, "bin", JavaHomeLayout.CompilerExecutableName)),
            VendorRaw = vendorRaw,
            Vendor = JvmIdentity.DetectVendor(vendorRaw),
            Distribution = JvmIdentity.DetectDistribution(
                NonEmpty(release.GetValueOrDefault("IMPLEMENTOR_VERSION")),
                vendorRaw,
                release.ContainsKey("GRAALVM_VERSION")),
        };
    }

    private static string? NonEmpty(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;
}
