namespace JdkFind.Providers;

/// <summary>
///     Scans jenv version-manager registrations at <c>&lt;root&gt;/versions</c>,
///     honoring <c>JENV_ROOT</c> with the default <c>~/.jenv</c>. The entries are
///     usually symbolic links to other managers' installations; the facade's
///     canonical-path deduplication folds them back onto the real home, with
///     <see cref="Jvm.Providers" /> listing every reporting source.
/// </summary>
public sealed class JenvJvmProvider(string? jenvRoot) : ICommonPrefixJvmProvider
{
    /// <inheritdoc />
    public string Name => "jenv";

    /// <summary>Scans the default jenv root (<c>JENV_ROOT</c> or <c>~/.jenv</c>).</summary>
    public JenvJvmProvider() : this(ResolveDefaultRoot()) { }

    /// <inheritdoc />
    public string? CommonPrefix => jenvRoot is null ? null : Path.Combine(jenvRoot, "versions");

    private static string? ResolveDefaultRoot()
    {
        var root = Environment.GetEnvironmentVariable("JENV_ROOT");
        if (!string.IsNullOrWhiteSpace(root))
            return root;

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(profile) ? null : Path.Combine(profile, ".jenv");
    }
}
