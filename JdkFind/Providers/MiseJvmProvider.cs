namespace JdkFind.Providers;

/// <summary>
///     Scans mise version-manager installs at <c>&lt;data dir&gt;/installs/java</c>,
///     honoring <c>MISE_DATA_DIR</c>, then <c>XDG_DATA_HOME</c>, with the default
///     <c>~/.local/share/mise</c>. Each version directory below is a candidate.
/// </summary>
public sealed class MiseJvmProvider(string? miseDataHome) : ICommonPrefixJvmProvider
{
    /// <inheritdoc />
    public string Name => "mise";

    /// <summary>Scans the default mise data home (<c>MISE_DATA_DIR</c>,
    /// <c>XDG_DATA_HOME</c>, or <c>~/.local/share/mise</c>).</summary>
    public MiseJvmProvider() : this(ResolveDefaultDataHome()) { }

    /// <inheritdoc />
    public string? CommonPrefix => miseDataHome is null ? null : Path.Combine(miseDataHome, "installs", "java");

    private static string? ResolveDefaultDataHome()
    {
        var dataDir = Environment.GetEnvironmentVariable("MISE_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(dataDir))
            return dataDir;

        var xdgData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(xdgData))
            return Path.Combine(xdgData, "mise");

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(profile) ? null : Path.Combine(profile, ".local", "share", "mise");
    }
}
