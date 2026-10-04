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
public sealed class LinuxJvmProvider(string? commonPrefix) : ICommonPrefixJvmProvider
{
    /// <inheritdoc />
    public string Name => OperatingSystem.IsFreeBSD() ? "freebsd" : "linux";

    /// <summary>Scans <c>/usr/lib/jvm</c> on Linux and <c>/usr/lib/jvm</c> plus
    /// <c>/usr/local</c> on FreeBSD; nothing elsewhere.</summary>
    public LinuxJvmProvider() : this(OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD() ? "/usr/lib/jvm" : (string?)null) { }

    /// <inheritdoc />
    public string? CommonPrefix => commonPrefix;

    private readonly string[]? injectedPrefixes;

    /// <summary>Test seam: scans exactly the given prefix directories.</summary>
    internal LinuxJvmProvider(string[] prefixes) : this((string?)null) => injectedPrefixes = prefixes;

    /// <inheritdoc />
    public IEnumerable<string> GetJavaHomes()
    {
        if (commonPrefix is not null)
            foreach (var home in JvmScanning.EnumerateGuarded(commonPrefix))
                yield return home;

        foreach (var prefix in injectedPrefixes ?? DefaultScanPrefixes())
            foreach (var home in JvmScanning.EnumerateGuarded(prefix))
                yield return home;
    }

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
