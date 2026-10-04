namespace JdkFind.Providers;

/// <summary>
///     Scans asdf version-manager installs at <c>&lt;ASDF_DATA_DIR&gt;/installs/java</c>,
///     honoring <c>ASDF_DATA_DIR</c> with the default <c>~/.asdf</c>. Each version
///     directory below is a candidate.
/// </summary>
public sealed class AsdfJvmProvider(string? asdfDataHome) : ICommonPrefixJvmProvider
{
    /// <inheritdoc />
    public string Name => "asdf";

    /// <summary>Scans the default asdf data home (<c>~/.asdf</c> or <c>ASDF_DATA_DIR</c>).</summary>
    public AsdfJvmProvider() : this(ResolveDefaultDataHome()) { }

    /// <inheritdoc />
    public string? CommonPrefix => asdfDataHome is null ? null : Path.Combine(asdfDataHome, "installs", "java");

    private static string? ResolveDefaultDataHome()
    {
        var dataDir = Environment.GetEnvironmentVariable("ASDF_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(dataDir))
            return dataDir;

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(profile) ? null : Path.Combine(profile, ".asdf");
    }
}
