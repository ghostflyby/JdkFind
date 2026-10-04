namespace JdkFind.Providers;

/// <summary>
///     Scans the Linux JVM directories: the conventional <c>/usr/lib/jvm</c> plus
///     the distribution locations (<c>/usr/java</c> Oracle RPM,
///     <c>/usr/lib64/jvm</c>, <c>/usr/lib32/jvm</c>, <c>/opt/jdk</c>,
///     <c>/opt/jdks</c>, <c>/opt/ibm</c>, flatpak's <c>/app/jdk</c>) and Gentoo's
///     named installs (<c>openjdk-*</c>/<c>openj9-*</c> under <c>/usr/lib</c>,
///     <c>/usr/lib64</c>, <c>/opt</c>). On FreeBSD the ports layout
///     (<c>/usr/local</c>, <c>openjdk*</c>) is scanned instead, and inside a snap
///     (<c>$SNAP</c>) the JVM directories are also scanned under the snap mount.
/// </summary>
public sealed class LinuxJvmProvider(string? commonPrefix) : ICommonPrefixJvmProvider
{
    /// <inheritdoc />
    public string Name => OperatingSystem.IsFreeBSD() ? "freebsd" : "linux";

    /// <summary>Scans <c>/usr/lib/jvm</c> on Linux and <c>/usr/lib/jvm</c> plus the
    /// ports layout on FreeBSD; nothing elsewhere.</summary>
    public LinuxJvmProvider() : this(OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD() ? "/usr/lib/jvm" : (string?)null) { }

    /// <inheritdoc />
    public string? CommonPrefix => commonPrefix;

    private readonly (string Prefix, string? Pattern)[]? injectedScans;

    /// <summary>Test seam: scans exactly the given (prefix, pattern) targets.</summary>
    internal LinuxJvmProvider((string Prefix, string? Pattern)[] scans) : this((string?)null) => injectedScans = scans;

    /// <inheritdoc />
    public IEnumerable<string> GetJavaHomes()
    {
        if (commonPrefix is not null)
            foreach (var home in JvmScanning.EnumerateGuarded(commonPrefix))
                yield return home;

        foreach (var (prefix, pattern) in injectedScans ?? DefaultScanTargets())
        {
            var homes = pattern is null
                ? JvmScanning.EnumerateGuarded(prefix)
                : JvmScanning.EnumerateGuarded(prefix, pattern);

            foreach (var home in homes)
                yield return home;
        }
    }

    private static IEnumerable<(string Prefix, string? Pattern)> DefaultScanTargets()
    {
        if (OperatingSystem.IsFreeBSD())
        {
            yield return ("/usr/local", "openjdk*");
            yield break;
        }

        if (!OperatingSystem.IsLinux())
            yield break;

        foreach (var prefix in new[] { "/usr/lib/jvm", "/usr/java", "/usr/lib64/jvm", "/usr/lib32/jvm", "/opt/jdk", "/opt/jdks", "/opt/ibm", "/app/jdk" })
            yield return (prefix, null);

        foreach (var prefix in new[] { "/usr/lib", "/usr/lib64", "/opt" })
        {
            yield return (prefix, "openjdk-*");
            yield return (prefix, "openj9-*");
        }

        // Inside a snap, mirrored system directories live under the $SNAP mount.
        var snap = Environment.GetEnvironmentVariable("SNAP");
        if (!string.IsNullOrWhiteSpace(snap))
            foreach (var prefix in new[] { "/usr/lib/jvm", "/usr/java", "/usr/lib64/jvm" })
                yield return (Path.Combine(snap.TrimEnd('/'), prefix.TrimStart('/')), null);
    }
}
