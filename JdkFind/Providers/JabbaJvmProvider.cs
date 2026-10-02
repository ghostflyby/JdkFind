namespace JdkFind.Providers;

/// <summary>
///     Scans Jabba installs at <c>&lt;JABBA_HOME&gt;/jdk/&lt;vendor&gt;/&lt;version&gt;</c>,
///     honoring <c>JABBA_HOME</c> with the default <c>~/.jabba</c>.
/// </summary>
public sealed class JabbaJvmProvider(string? home) : JvmProviderBase
{
    public override string Name => "jabba";

    public JabbaJvmProvider() : this(ResolveDefaultHome()) { }

    public override IEnumerable<string> GetJavaHomes() =>
        home != null ? ScanNested([Path.Combine(home, "jdk")]) : Enumerable.Empty<string>();

    private static string? ResolveDefaultHome()
    {
        var jabbaHome = Environment.GetEnvironmentVariable("JABBA_HOME");
        if (!string.IsNullOrWhiteSpace(jabbaHome))
            return jabbaHome;

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(profile) ? null : Path.Combine(profile, ".jabba");
    }
}
