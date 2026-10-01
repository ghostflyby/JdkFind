using System.Runtime.CompilerServices;

namespace JdkFind;

/// <summary>Facade for locating JVM installations across all configured providers.</summary>
public static class JdkFinder
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    /// <summary>Streams JVMs from all configured providers, in provider order.</summary>
    public static IAsyncEnumerable<Jvm> LocateAsync(JdkFindOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new JdkFindOptions();
        return Enumerate(options, cancellationToken);
    }

    /// <summary>Streams only the JVMs whose feature version matches.</summary>
    public static IAsyncEnumerable<Jvm> FindAsync(int languageVersion, JdkFindOptions? options = null, CancellationToken cancellationToken = default) =>
        LocateAsync(options, cancellationToken).Where(jvm => jvm.LanguageVersion == languageVersion);

    /// <summary>Returns the newest JVM found, or null when none is found.</summary>
    public static async ValueTask<Jvm?> GetNewestAsync(JdkFindOptions? options = null, CancellationToken cancellationToken = default)
    {
        Jvm? newest = null;
        await foreach (var jvm in LocateAsync(options, cancellationToken))
            if (JvmVersionComparer.Default.Compare(jvm, newest) > 0)
                newest = jvm;

        return newest;
    }

    /// <summary>
    ///     Returns the JVM pointed to by <c>JAVA_HOME</c> when it is among the results,
    ///     otherwise the newest JVM found. Returns null when none is found.
    /// </summary>
    public static async ValueTask<Jvm?> GetDefaultAsync(JdkFindOptions? options = null, CancellationToken cancellationToken = default)
    {
        // Single pass: when JAVA_HOME misses, take the newest from the same results instead of a second full enumeration.
        var jvms = await LocateAsync(options, cancellationToken).ToListAsync(cancellationToken);

        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(javaHome))
        {
            var javaHomeKey = GetDeduplicationKey(javaHome);
            foreach (var jvm in jvms)
                if (PathComparer.Equals(GetDeduplicationKey(jvm.Home.FullName), javaHomeKey))
                    return jvm;
        }

        Jvm? newest = null;
        foreach (var jvm in jvms)
            if (JvmVersionComparer.Default.Compare(jvm, newest) > 0)
                newest = jvm;

        return newest;
    }

    private static async IAsyncEnumerable<Jvm> Enumerate(
        JdkFindOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var seen = options.DeduplicateHomes ? new HashSet<string>(PathComparer) : null;

        foreach (var provider in options.Providers)
            await foreach (var candidate in provider.GetJavaHomesAsync(cancellationToken))
            {
                if (seen is not null && !seen.Add(GetDeduplicationKey(candidate)))
                    continue;

                if (CreateJvm(candidate, provider.Name) is { } jvm)
                    yield return jvm;
            }
    }

    private static Jvm? CreateJvm(string homePath, string providerName)
    {
        var releaseFilePath = Path.Combine(homePath, "release");
        if (!File.Exists(releaseFilePath))
            return null;

        IReadOnlyDictionary<string, string> release;
        try
        {
            release = ReleaseFile.Parse(releaseFilePath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // An unreadable release file (access denial, ...) counts as no JVM; one bad directory must not kill the scan.
            return null;
        }

        var version = release.GetValueOrDefault("JAVA_VERSION");
        return new Jvm
        {
            Home = new DirectoryInfo(homePath),
            Provider = providerName,
            Version = version,
            LanguageVersion = ReleaseFile.TryGetLanguageVersion(version),
            Vendor = release.GetValueOrDefault("IMPLEMENTOR"),
            Architecture = release.GetValueOrDefault("OS_ARCH"),
            OsName = release.GetValueOrDefault("OS_NAME"),
        };
    }

    /// <summary>
    ///     Deduplication key: the canonical path of the home (every path component's
    ///     symbolic link expanded, collapsing Homebrew's opt → Cellar and macOS
    ///     <c>/Library/Java</c> aliases); compared case-insensitively on the
    ///     case-insensitive platforms.
    /// </summary>
    private static string GetDeduplicationKey(string path)
    {
        try
        {
            return ResolveSymlinks(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)));
        }
        catch (Exception exception) when (exception is IOException or System.Security.SecurityException or ArgumentException)
        {
            return path;
        }
    }

    /// <summary>Expands every symbolic link along the path, bounding the chain to avoid link loops.</summary>
    internal static string ResolveSymlinks(string fullPath, int remainingHops = 32)
    {
        if (remainingHops == 0)
            return fullPath;

        var root = Path.GetPathRoot(fullPath) ?? string.Empty;
        var segments = fullPath[root.Length..].Split('/', '\\', StringSplitOptions.RemoveEmptyEntries);
        var resolved = root;
        for (var index = 0; index < segments.Length; index++)
        {
            resolved = Path.Combine(resolved, segments[index]);
            string? target;
            try
            {
                target = new DirectoryInfo(resolved).LinkTarget;
            }
            catch (Exception exception) when (exception is IOException or System.Security.SecurityException)
            {
                return fullPath;
            }

            if (target is null)
                continue;

            // The link target may be relative, and the joined remainder may still contain links; re-resolve.
            var targetFull = Path.GetFullPath(target, Path.GetDirectoryName(resolved)!);
            var rest = string.Join(Path.DirectorySeparatorChar, segments[(index + 1)..]);
            var combined = rest.Length == 0 ? targetFull : Path.Combine(targetFull, rest);
            return ResolveSymlinks(combined, remainingHops - 1);
        }

        return fullPath;
    }
}
