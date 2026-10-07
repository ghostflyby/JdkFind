namespace JdkFind;

/// <summary>
///     A source of candidate Java home directories. Every path returned from
///     <see cref="GetJavaHomes" /> must be a validated home — i.e. pass
///     <see cref="JavaHomeLayout.Probe" />; the capability interfaces enforce this by
///     default through their validation seam. The facade deduplicates by canonical
///     path and extracts metadata — it does not re-validate.
///     Enumeration is synchronous by design: every source is local file-system or
///     registry I/O measured in milliseconds, and the BCL has no async
///     directory-enumeration API to make it real.
/// </summary>
public interface IJvmProvider
{
    /// <summary>Stable lowercase identifier of this provider (e.g. <c>java-home</c>, <c>sdkman</c>).</summary>
    string Name { get; }

    /// <summary>
    ///     Enumerates validated Java home directories. Nonexistent locations must yield
    ///     an empty sequence, and every returned path must pass
    ///     <see cref="JavaHomeLayout.Probe" />.
    /// </summary>
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
    IEnumerable<string> IJvmProvider.GetJavaHomes() =>
        GetCommonPrefixes()
            .SelectMany(prefix => JvmScanning.EnumerateGuarded(prefix, SearchPattern))
            .Select(GetJavaHome)
            .OfType<string>();

    /// <summary>
    ///     Optional search pattern applied when enumerating a prefix's children
    ///     (e.g. Homebrew's <c>openjdk*</c>); null enumerates all children.
    /// </summary>
    protected string? SearchPattern => null;

    /// <summary>The prefix directories whose immediate subdirectories are candidates.</summary>
    protected IEnumerable<string> GetCommonPrefixes();

    /// <summary>Maps a scanned subdirectory to the Java home inside it, or null when it is not one.
    /// Defaults to <see cref="JavaHomeLayout.Probe" />; implementations overriding it can
    /// call the probe directly and add extra checks.</summary>
    protected string? GetJavaHome(string subDirectory) => JavaHomeLayout.Probe(subDirectory);
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
        if (CommonPrefix is null)
            return [];
        return [CommonPrefix];
    }
}
