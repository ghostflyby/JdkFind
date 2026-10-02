namespace JdkFind;

/// <summary>
///     Guarded directory enumeration shared by the providers whose candidate layout
///     does not fit the prefix-scan capability interfaces (nested tool directories).
/// </summary>
internal static class JvmScanning
{
    /// <summary>
    ///     Scans each prefix two levels deep: when <paramref name="leafName" /> is given only
    ///     that fixed leaf (e.g. Scoop's <c>current</c>) is probed; otherwise every second-level
    ///     directory (e.g. Jabba's <c>jdk/&lt;vendor&gt;/&lt;version&gt;</c>) is probed.
    ///     Missing or unreadable directories are skipped silently.
    /// </summary>
    internal static IEnumerable<string> ScanNested(IEnumerable<string> prefixes, string? leafName = null)
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
                    if (Directory.Exists(leaf) && JavaHomeLayout.Probe(leaf) is { } javaHome)
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
                    if (JavaHomeLayout.Probe(level2) is { } javaHome)
                        yield return javaHome;
            }
        }
    }
}
