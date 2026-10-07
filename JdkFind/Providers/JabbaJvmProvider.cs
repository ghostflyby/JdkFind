namespace JdkFind.Providers;

/// <summary>
///     Scans Jabba installs at <c>&lt;JABBA_HOME&gt;/jdk/&lt;vendor&gt;/&lt;version&gt;</c>,
///     honoring <c>JABBA_HOME</c> with the default <c>~/.jabba</c>. The vendor
///     directories are the prefixes; every version directory below them is a candidate.
/// </summary>
public sealed class JabbaJvmProvider(string? home) : ICommonPrefixesJvmProvider
{
    /// <inheritdoc />
    public string Name => "jabba";

    /// <summary>Scans the default Jabba home (or <c>JABBA_HOME</c>).</summary>
    public JabbaJvmProvider() : this(ResolveDefaultHome())
    {
    }

    IEnumerable<string> ICommonPrefixesJvmProvider.GetCommonPrefixes() =>
        home != null ? JvmScanning.EnumerateGuarded(Path.Combine(home, "jdk")) : [];

    private static string? ResolveDefaultHome()
    {
        var jabbaHome = Environment.GetEnvironmentVariable("JABBA_HOME");
        if (!string.IsNullOrWhiteSpace(jabbaHome))
            return jabbaHome;

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(profile) ? null : Path.Combine(profile, ".jabba");
    }
}
