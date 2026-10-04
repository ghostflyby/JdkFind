namespace JdkFind.Providers;

/// <summary>
///     Scans the Linux JVM directories: the conventional <c>/usr/lib/jvm</c> plus
///     the distribution locations (<c>/usr/java</c> Oracle RPM,
///     <c>/usr/lib64/jvm</c>, <c>/usr/lib32/jvm</c>, <c>/opt/jdk</c>,
///     <c>/opt/jdks</c>, <c>/opt/ibm</c>, flatpak's <c>/app/jdk</c>, Gentoo's
///     installs under <c>/usr/lib</c>, <c>/usr/lib64</c>, <c>/opt</c>). On FreeBSD
///     <c>/usr/local</c> is scanned, and inside a snap (<c>$SNAP</c>) the JVM
///     directories are also scanned under the snap mount.
/// </summary>
public sealed class LinuxJvmProvider : ICommonPrefixJvmProvider, ICommonPrefixesJvmProvider
{
    private readonly string? commonPrefix;
    private readonly string[] scanPrefixes;

    /// <summary>Scans the full platform list on Linux and <c>/usr/local</c> on
    /// FreeBSD; nothing elsewhere.</summary>
    public LinuxJvmProvider()
    {
        commonPrefix = OperatingSystem.IsFreeBSD() ? "/usr/local" : "/usr/lib/jvm";
        scanPrefixes = DefaultScanPrefixes().ToArray();
    }

    /// <summary>Scans exactly one prefix directory — the injection seam for callers
    /// (and tests) that control the candidate location themselves.</summary>
    public LinuxJvmProvider(string? commonPrefix)
    {
        this.commonPrefix = commonPrefix;
        scanPrefixes = commonPrefix is null ? [] : [commonPrefix];
    }

    /// <summary>Test seam: scans exactly the given prefix directories.</summary>
    internal LinuxJvmProvider(string[] prefixes)
    {
        commonPrefix = prefixes.FirstOrDefault(OperatingSystem.IsFreeBSD() ? "/usr/local" : "/usr/lib/jvm");
        scanPrefixes = prefixes;
    }

    /// <inheritdoc />
    public string Name => OperatingSystem.IsFreeBSD() ? "freebsd" : "linux";

    /// <summary>The conventional directory of this provider, or null when the
    /// platform is not covered.</summary>
    public string? CommonPrefix => commonPrefix;

    IEnumerable<string> ICommonPrefixesJvmProvider.GetCommonPrefixes() => scanPrefixes;

    private static IEnumerable<string> DefaultScanPrefixes()
    {
        if (OperatingSystem.IsFreeBSD())
        {
            yield return "/usr/local";
            yield break;
        }

        if (!OperatingSystem.IsLinux())
            yield break;

        foreach (var prefix in new[] { "/usr/lib/jvm", "/usr/java", "/usr/lib64/jvm", "/usr/lib32/jvm", "/opt/jdk", "/opt/jdks", "/opt/ibm", "/app/jdk", "/usr/lib", "/usr/lib64", "/opt" })
            yield return prefix;

        // Inside a snap, mirrored system directories live under the $SNAP mount.
        var snap = Environment.GetEnvironmentVariable("SNAP");
        if (!string.IsNullOrWhiteSpace(snap))
            foreach (var prefix in new[] { "/usr/lib/jvm", "/usr/java", "/usr/lib64/jvm" })
                yield return Path.Combine(snap.TrimEnd('/'), prefix.TrimStart('/'));
    }
}
