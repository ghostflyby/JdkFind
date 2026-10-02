namespace JdkFind.Providers;

/// <summary>
///     Scans the per-user macOS JVM directory <c>~/Library/Java/JavaVirtualMachines</c>.
///     Together with <see cref="MacOsSystemJvmProvider" /> this covers everything
///     <c>/usr/libexec/java_home</c> reports, without spawning it.
/// </summary>
public sealed class MacOsUserJvmProvider(string? commonPrefix) : JvmProviderBase
{
    public override string Name => "macos-user";

    public MacOsUserJvmProvider() : this(ResolveDefaultPrefix()) { }

    public override IEnumerable<string> GetJavaHomes() =>
        commonPrefix != null ? ScanPrefixes([commonPrefix]) : Enumerable.Empty<string>();

    private static string? ResolveDefaultPrefix()
    {
        if (!OperatingSystem.IsMacOS())
            return null;

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(profile) ? null : Path.Combine(profile, "Library", "Java", "JavaVirtualMachines");
    }
}
