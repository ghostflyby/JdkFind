namespace JdkFind.Providers;

/// <summary>
///     Scans JDK toolchains Gradle auto-provisioned into <c>&lt;GRADLE_USER_HOME&gt;/jdks</c>,
///     honoring <c>GRADLE_USER_HOME</c> with the default <c>~/.gradle</c>.
/// </summary>
public sealed class GradleJvmProvider(string? gradleUserHome) : JvmProviderBase
{
    public override string Name => "gradle";

    public GradleJvmProvider() : this(ResolveDefaultHome()) { }

    public override IEnumerable<string> GetJavaHomes() =>
        gradleUserHome != null ? ScanPrefixes([Path.Combine(gradleUserHome, "jdks")]) : Enumerable.Empty<string>();

    private static string? ResolveDefaultHome()
    {
        var gradleUserHome = Environment.GetEnvironmentVariable("GRADLE_USER_HOME");
        if (!string.IsNullOrWhiteSpace(gradleUserHome))
            return gradleUserHome;

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(profile) ? null : Path.Combine(profile, ".gradle");
    }
}
