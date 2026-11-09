namespace JdkFind.Providers;

/// <summary>
///     Scans the JVMs coursier installs under its cache's <c>jvm</c> directory
///     (the <c>jvm/&lt;name&gt;/&lt;version&gt;</c> shape, e.g.
///     <c>jvm/temurin-21/21.0.5+11</c>). Resolution order:
///     <c>COURSIER_JVM_CACHE</c>, then <c>COURSIER_CACHE</c> with <c>jvm</c>
///     appended, then the platform defaults — <c>%LOCALAPPDATA%\Coursier\jvm</c>
///     on Windows, <c>~/Library/Caches/Coursier/jvm</c> on macOS, and
///     <c>$XDG_CACHE_HOME/coursier/jvm</c> (defaulting to <c>~/.cache</c>)
///     elsewhere.
/// </summary>
public sealed class CoursierJvmProvider(string? jvmDirectory) : IJvmProvider
{
    /// <inheritdoc />
    public string Name => "coursier";

    /// <summary>Scans the default coursier JVM cache directory.</summary>
    public CoursierJvmProvider() : this(ResolveDefaultJvmDirectory()) { }

    IEnumerable<string> IJvmProvider.GetJavaHomes() =>
        JvmScanning.EnumerateGuarded(jvmDirectory ?? string.Empty)
            .SelectMany(JvmScanning.ProbeNested);

    private static string? ResolveDefaultJvmDirectory()
    {
        var jvmCache = Environment.GetEnvironmentVariable("COURSIER_JVM_CACHE");
        if (!string.IsNullOrWhiteSpace(jvmCache))
            return jvmCache;

        var cache = Environment.GetEnvironmentVariable("COURSIER_CACHE");
        if (!string.IsNullOrWhiteSpace(cache))
            return Path.Combine(cache, "jvm");

        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return string.IsNullOrEmpty(localAppData) ? null : Path.Combine(localAppData, "Coursier", "jvm");
        }

        if (OperatingSystem.IsMacOS())
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrEmpty(profile) ? null : Path.Combine(profile, "Library", "Caches", "Coursier", "jvm");
        }

        var cacheHome = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        if (string.IsNullOrWhiteSpace(cacheHome))
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            cacheHome = string.IsNullOrEmpty(profile) ? null : Path.Combine(profile, ".cache");
        }

        return string.IsNullOrWhiteSpace(cacheHome) ? null : Path.Combine(cacheHome, "coursier", "jvm");
    }
}
