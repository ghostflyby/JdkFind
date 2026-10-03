namespace JdkFind.Providers;

/// <summary>
///     Scans JDK toolchains Gradle auto-provisioned into <c>&lt;GRADLE_USER_HOME&gt;/jdks</c>,
///     honoring <c>GRADLE_USER_HOME</c> with the default <c>~/.gradle</c>.
/// </summary>
public sealed class GradleJvmProvider(string? gradleUserHome) : ICommonPrefixJvmProvider
{
    /// <inheritdoc />
    public string Name => "gradle";

    /// <summary>Scans the default Gradle user home (or <c>GRADLE_USER_HOME</c>).</summary>
    public GradleJvmProvider() : this(ResolveDefaultHome()) { }

    /// <inheritdoc />
    public string? CommonPrefix =>
        gradleUserHome != null ? Path.Combine(gradleUserHome, "jdks") : null;

    private static string? ResolveDefaultHome()
    {
        var gradleUserHome = Environment.GetEnvironmentVariable("GRADLE_USER_HOME");
        if (!string.IsNullOrWhiteSpace(gradleUserHome))
            return gradleUserHome;

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(profile) ? null : Path.Combine(profile, ".gradle");
    }
}
