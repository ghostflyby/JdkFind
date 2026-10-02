namespace JdkFind;

/// <summary>
///     Shared plumbing for directory-scanning providers. Enumeration is synchronous
///     by design: every source is local file-system or registry I/O measured in
///     milliseconds, and the BCL has no async directory-enumeration API to make it
///     real. Concrete providers are plain iterators.
/// </summary>
public abstract class JvmProviderBase : IJvmProvider
{
    public abstract string Name { get; }

    public abstract IEnumerable<string> GetJavaHomes();

    /// <summary>Probes a candidate directory for a Java home under any known layout.</summary>
    protected static string? Probe(string candidateDirectory) => JavaHomeLayout.Probe(candidateDirectory);

    /// <summary>
    ///     Scans each prefix directory and yields probed Java homes of its immediate
    ///     subdirectories. Missing or unreadable prefixes are skipped silently.
    /// </summary>
    protected static IEnumerable<string> ScanPrefixes(IEnumerable<string> prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (!Directory.Exists(prefix))
                continue;

            // Read failures such as access denial count as missing; one bad directory must not kill the whole scan.
            string[] subDirectories;
            try
            {
                subDirectories = [.. Directory.EnumerateDirectories(prefix)];
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                continue;
            }

            foreach (var subDirectory in subDirectories)
                if (Probe(subDirectory) is { } javaHome)
                    yield return javaHome;
        }
    }

    /// <summary>
    ///     Scans each prefix two levels deep: when <paramref name="leafName" /> is given only
    ///     that fixed leaf (e.g. Scoop's <c>current</c>) is probed; otherwise every second-level
    ///     directory (e.g. Jabba's <c>jdk/&lt;vendor&gt;/&lt;version&gt;</c>) is probed.
    ///     Missing or unreadable directories are skipped silently.
    /// </summary>
    protected static IEnumerable<string> ScanNested(IEnumerable<string> prefixes, string? leafName = null)
    {
        foreach (var prefix in prefixes)
        {
            if (!Directory.Exists(prefix))
                continue;

            string[] level1Directories;
            try
            {
                level1Directories = [.. Directory.EnumerateDirectories(prefix)];
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                continue;
            }

            foreach (var level1 in level1Directories)
            {
                if (leafName is not null)
                {
                    var leaf = Path.Combine(level1, leafName);
                    if (Directory.Exists(leaf) && Probe(leaf) is { } javaHome)
                        yield return javaHome;
                    continue;
                }

                string[] level2Directories;
                try
                {
                    level2Directories = [.. Directory.EnumerateDirectories(level1)];
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    continue;
                }

                foreach (var level2 in level2Directories)
                    if (Probe(level2) is { } javaHome)
                        yield return javaHome;
            }
        }
    }
}
