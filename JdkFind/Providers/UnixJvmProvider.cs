namespace JdkFind.Providers;

/// <summary>
///     Scans the fixed unix JVM directories: on Linux /usr/lib/jvm, the
///     distribution locations (/usr/java Oracle RPM, /usr/lib64/jvm,
///     /usr/lib32/jvm, /opt/jdk, /opt/jdks, /opt/ibm,
///     Gentoo's installs under /usr/lib, /usr/lib64, /opt) and $SNAP mirrors; on
///     FreeBSD and OpenBSD /usr/local. Source name: unix.
/// </summary>
public sealed class UnixJvmProvider : IJvmProvider
{
    private readonly string[]? injectedPrefixes;

    /// <summary>Scans the full platform list on the current unix-like platform.</summary>
    public UnixJvmProvider()
    {
    }

    /// <summary>Test seam: scans exactly the given prefix directories.</summary>
    internal UnixJvmProvider(string[] prefixes) => injectedPrefixes = prefixes;

    /// <summary>Scans exactly one prefix directory — the injection seam for callers
    /// (and tests) that control the candidate location themselves. A null prefix
    /// means nothing to scan.</summary>
    public UnixJvmProvider(string? commonPrefix) => injectedPrefixes = commonPrefix is null ? [] : [commonPrefix];

    /// <inheritdoc />
    public string Name => "unix";

    /// <inheritdoc />
    public IEnumerable<string> GetJavaHomes()
    {
        foreach (var prefix in injectedPrefixes ?? ResolvePrefixes())
            foreach (var candidate in JvmScanning.EnumerateGuarded(prefix))
                if (JavaHomeLayout.Probe(candidate) is { } home)
                    yield return home;
    }

    private static IEnumerable<string> ResolvePrefixes()
    {
        if (OperatingSystem.IsFreeBSD() || IsOpenBsd())
        {
            yield return "/usr/local";
            yield break;
        }

        if (!OperatingSystem.IsLinux())
            yield break;

        foreach (var prefix in new[]
                 {
                     "/usr/lib/jvm", "/usr/java", "/usr/lib64/jvm", "/usr/lib32/jvm", "/opt/jdk", "/opt/jdks",
                     "/opt/ibm", "/usr/lib", "/usr/lib64", "/opt"
                 })
            yield return prefix;

        // Inside a snap, mirrored system directories live under the $SNAP mount.
        var snap = Environment.GetEnvironmentVariable("SNAP");
        if (!string.IsNullOrWhiteSpace(snap))
            foreach (var prefix in new[] { "/usr/lib/jvm", "/usr/java", "/usr/lib64/jvm" })
                yield return Path.Combine(snap.TrimEnd('/'), prefix.TrimStart('/'));
    }

#pragma warning disable CA1418 // "OpenBSD" is not a platform name the analyzer knows
    private static bool IsOpenBsd() => OperatingSystem.IsOSPlatform("OpenBSD");
#pragma warning restore CA1418
}
