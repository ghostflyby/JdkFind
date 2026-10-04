namespace JdkFind.Providers;

/// <summary>
///     Scans the fixed system JVM directories of the unix-like platforms: on macOS
///     the system, machine and per-user JavaVirtualMachines directories; on Linux
///     /usr/lib/jvm, the distribution locations (/usr/java Oracle RPM,
///     /usr/lib64/jvm, /usr/lib32/jvm, /opt/jdk, /opt/jdks, /opt/ibm, flatpak's
///     /app/jdk, Gentoo's installs under /usr/lib, /usr/lib64, /opt) and $SNAP
///     mirrors; on FreeBSD /usr/local. Nonexistent locations yield nothing, so one
///     provider covers every unix platform. Source name: unix.
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
    /// (and tests) that control the candidate location themselves.</summary>
    public UnixJvmProvider(string? commonPrefix) => injectedPrefixes = commonPrefix is null ? null : [commonPrefix];

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
        if (OperatingSystem.IsMacOS())
        {
            yield return "/System/Library/Java/JavaVirtualMachines";
            yield return "/Library/Java/JavaVirtualMachines";
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(profile))
                yield return Path.Combine(profile, "Library", "Java", "JavaVirtualMachines");

            yield break;
        }

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
