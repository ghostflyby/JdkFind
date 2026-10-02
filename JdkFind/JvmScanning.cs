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
            yield break;

        string[] subDirectories;
        try
        {
            subDirectories = [.. Directory.EnumerateDirectories(directory, searchPattern ?? "*")];
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            yield break;
        }

        foreach (var subDirectory in subDirectories)
            yield return subDirectory;
    }
}
