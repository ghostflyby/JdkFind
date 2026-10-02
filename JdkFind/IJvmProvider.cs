namespace JdkFind;

/// <summary>
///     A source of candidate Java home directories. Implementations only enumerate
///     candidate paths; validation and metadata extraction are done by <see cref="JdkFinder" />.
///     Enumeration is synchronous by design: every source is local file-system or
///     registry I/O measured in milliseconds, and the BCL has no async
///     directory-enumeration API to make it real.
/// </summary>
public interface IJvmProvider
{
    /// <summary>Stable lowercase identifier of this provider (e.g. <c>java-home</c>, <c>sdkman</c>).</summary>
    string Name { get; }

    /// <summary>Enumerates candidate Java home directories. Nonexistent locations must yield an empty sequence.</summary>
    IEnumerable<string> GetJavaHomes();
}

/// <summary>
///     Capability mixin for providers that derive candidates by scanning prefix
///     directories and validating each immediate subdirectory. Implementors supply the
///     prefixes and the per-subdirectory validation; the enumeration itself — including
///     the missing-or-unreadable-prefix guards — is provided here.
/// </summary>
public interface ICommonPrefixesJvmProvider : IJvmProvider
{
    IEnumerable<string> IJvmProvider.GetJavaHomes()
    {
        foreach (var prefix in GetCommonPrefixes())
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
                if (GetJavaHome(subDirectory) is { } javaHome)
                    yield return javaHome;
        }
    }

    /// <summary>The prefix directories whose immediate subdirectories are candidates.</summary>
    protected IEnumerable<string> GetCommonPrefixes();

    /// <summary>Maps a scanned subdirectory to the Java home inside it, or null when it is not one.</summary>
    protected string? GetJavaHome(string subDirectory);
}

/// <summary>
///     Capability mixin for providers with a single prefix directory. The prefix is the
///     only piece implementors supply — null means "nothing to scan" (unsupported
///     platform, tool not installed); subdirectory validation defaults to probing the
///     known layouts (plain, macOS bundle, Homebrew keg) via <see cref="JavaHomeLayout" />.
/// </summary>
public interface ICommonPrefixJvmProvider : ICommonPrefixesJvmProvider
{
    /// <summary>The single prefix directory to scan, or null when there is nothing to scan.</summary>
    protected string? CommonPrefix { get; }

    IEnumerable<string> ICommonPrefixesJvmProvider.GetCommonPrefixes()
    {
        if (CommonPrefix != null)
            yield return CommonPrefix;
    }

    string? ICommonPrefixesJvmProvider.GetJavaHome(string subDirectory) => JavaHomeLayout.Probe(subDirectory);
}
