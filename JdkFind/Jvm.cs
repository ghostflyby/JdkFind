namespace JdkFind;

/// <summary>
///     A located JVM installation. Core metadata comes from the JEP 223 <c>release</c>
///     file; runtime properties (when probed) come from executing the installation's
///     own java executable.
/// </summary>
public sealed record Jvm
{
    /// <summary>The Java home directory.</summary>
    public required DirectoryInfo Home { get; init; }

    /// <summary>Identifiers of every provider that reported this home, in provider order.</summary>
    public required IReadOnlyList<string> Providers { get; init; }

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
    ///     Why the java executable could not be started — a spawn error, the
    ///     probe timeout, or (on <see cref="JdkFinder.FromExecutable" /> /
    ///     <see cref="JdkFinder.FromHome" />, which require a successful exit) a
    ///     non-zero exit carrying the output tail — or null when it ran, reported
    ///     nothing parseable, or no probe ran.
    /// </summary>
    public string? StartFailure { get; init; }

    /// <summary>
    ///     True when the installation ships a compiler (<c>bin/javac</c>), i.e. it is a
    ///     JDK rather than a runtime-only image (standalone JREs, jlink runtimes).
    /// </summary>
    public bool HasCompiler { get; init; }

    /// <summary>
    ///     The raw vendor string. Discovery prefers the release file's
    ///     <c>IMPLEMENTOR</c> with the probed <c>java.vendor</c> as fallback; the
    ///     probe factories prefer the binary's word and let the release file fill
    ///     the gap.
    /// </summary>
    public string? VendorRaw { get; init; }

    /// <summary>The normalized upstream vendor the raw string matched, or <see cref="JvmVendor.Unknown" />.</summary>
    public JvmVendor Vendor { get; init; }

    /// <summary>The distribution per the foojay API naming, or <see cref="JvmDistribution.Unknown" />.</summary>
    public JvmDistribution Distribution { get; init; }

    /// <summary><c>java.runtime.name</c> from the runtime probe; null when no probe
    /// ran or the binary did not report it.</summary>
    public string? RuntimeName { get; init; }

    /// <summary><c>java.runtime.version</c> from the runtime probe (includes build metadata).</summary>
    public string? RuntimeVersion { get; init; }

    /// <summary><c>java.vm.name</c> from the runtime probe.</summary>
    public string? VmName { get; init; }

    /// <summary><c>java.vm.version</c> from the runtime probe.</summary>
    public string? VmVersion { get; init; }

    /// <summary>Raw <c>OS_NAME</c> value, e.g. <c>Darwin</c>.</summary>
    public string? OsName { get; init; }

    /// <summary>Raw <c>OS_ARCH</c> value or the probed <c>os.arch</c>, by the same
    /// precedence as <see cref="VendorRaw" /> (discovery prefers the release file;
    /// probing prefers the binary).</summary>
    public string? Architecture { get; init; }

    /// <summary>Summarizes the installation: language version, full version, distribution and home.</summary>
    public override string ToString() =>
        $"{LanguageVersion?.ToString() ?? "?"} ({Version}) {Distribution} — {Home.FullName}";
}
