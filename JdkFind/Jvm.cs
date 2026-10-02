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
    ///     True when the installation ships a compiler (<c>bin/javac</c>), i.e. it is a
    ///     JDK rather than a runtime-only image (standalone JREs, jlink runtimes).
    /// </summary>
    public bool HasCompiler { get; init; }

    /// <summary>The raw vendor string (<c>IMPLEMENTOR</c>, falling back to the probed <c>java.vendor</c>).</summary>
    public string? Vendor { get; init; }

    /// <summary>The known vendor the raw string matched, or <see cref="JvmVendor.Unknown" />.</summary>
    public JvmVendor KnownVendor { get; init; }

    /// <summary>Human-friendly vendor display name; unknown vendors show their raw string.</summary>
    public string VendorDisplayName { get; init; } = "Unknown";

    /// <summary><c>java.runtime.name</c> from the runtime probe; null when probing is off or failed.</summary>
    public string? RuntimeName { get; init; }

    /// <summary><c>java.runtime.version</c> from the runtime probe (includes build metadata).</summary>
    public string? RuntimeVersion { get; init; }

    /// <summary><c>java.vm.name</c> from the runtime probe.</summary>
    public string? VmName { get; init; }

    /// <summary><c>java.vm.version</c> from the runtime probe.</summary>
    public string? VmVersion { get; init; }

    /// <summary>Raw <c>OS_NAME</c> value, e.g. <c>Darwin</c>.</summary>
    public string? OsName { get; init; }

    /// <summary>Raw <c>OS_ARCH</c> value, falling back to the probed <c>os.arch</c>.</summary>
    public string? Architecture { get; init; }

    public override string ToString() =>
        $"{LanguageVersion?.ToString() ?? "?"} ({Version}) {VendorDisplayName} — {Home.FullName}";
}
