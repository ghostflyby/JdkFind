namespace JdkFind.Providers;

/// <summary>
///     Scans the directory IntelliJ IDEA downloads JDKs into: <c>~/.jdks</c>. On macOS
///     the downloads land in the per-user JVM directory, which is
///     <see cref="MacOsUserJvmProvider" />'s territory, so this provider no-ops there.
/// </summary>
public sealed class IntelliJJvmProvider(string? commonPrefix) : ICommonPrefixJvmProvider
{
    public string Name => "intellij";

    public IntelliJJvmProvider() : this(ResolveDefaultPrefix()) { }

    public string? CommonPrefix => commonPrefix;

    private static string? ResolveDefaultPrefix()
    {
        if (OperatingSystem.IsMacOS())
            return null;

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(profile) ? null : Path.Combine(profile, ".jdks");
    }
}
