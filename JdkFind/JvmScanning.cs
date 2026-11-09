using System.Security;

namespace JdkFind;

/// <summary>
///     Guarded directory enumeration shared by the prefix-scan machinery and the
///     providers whose prefixes are themselves computed by listing a directory.
/// </summary>
internal static class JvmScanning
{
    /// <summary>
    ///     Enumerates a directory's immediate subdirectories. Missing or unreadable
    ///     directories yield nothing — one bad directory must not kill the whole scan.
    /// </summary>
    internal static IEnumerable<string> EnumerateGuarded(string directory, string? searchPattern = null)
    {
        if (!Directory.Exists(directory))
            return [];

        try
        {
            return [.. Directory.EnumerateDirectories(directory, searchPattern ?? "*")];
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return [];
        }
    }

    /// <summary>
    ///     Yields the candidate's home when it is one itself; otherwise the homes
    ///     found among its immediate subdirectories. This is the two-level shape
    ///     the nested caches use — coursier's <c>jvm/&lt;name&gt;/&lt;version&gt;</c>
    ///     and the per-user Windows <c>Programs/&lt;vendor&gt;/&lt;version&gt;</c> —
    ///     while keeping flat layouts (the candidate is the home) working too.
    /// </summary>
    internal static IEnumerable<string> ProbeNested(string candidateDirectory)
    {
        if (JavaHomeLayout.Probe(candidateDirectory) is { } home)
        {
            yield return home;
            yield break;
        }

        foreach (var child in EnumerateGuarded(candidateDirectory))
            if (JavaHomeLayout.Probe(child) is { } childHome)
                yield return childHome;
    }
}
