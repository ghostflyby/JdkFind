namespace JdkFind.Providers;

/// <summary>
///     Scans the directory IntelliJ IDEA downloads JDKs into: <c>~/.jdks</c>. On macOS
///     the downloads land in the per-user JVM directory, which is
///     <see cref="MacOsUserJvmProvider" />'s territory, so this provider no-ops there.
/// </summary>
public sealed class IntelliJJvmProvider(string? commonPrefix) : JvmProviderBase
{
    public override string Name => "intellij";

    public IntelliJJvmProvider() : this(ResolveDefaultPrefix()) { }

    public override IEnumerable<string> GetJavaHomes() =>
        commonPrefix != null ? ScanPrefixes([commonPrefix]) : Enumerable.Empty<string>();

    private static string? ResolveDefaultPrefix()
    {
        if (OperatingSystem.IsMacOS())
            return null;

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(profile) ? null : Path.Combine(profile, ".jdks");
    }
}
