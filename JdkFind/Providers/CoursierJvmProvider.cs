namespace JdkFind.Providers;

/// <summary>
///     Scans the JVMs coursier installs under its cache's <c>jvm</c> directory
///     (the <c>jvm/&lt;name&gt;/&lt;version&gt;</c> shape, e.g.
///     <c>jvm/temurin-21/21.0.5+11</c>). <c>COURSIER_JVM_CACHE</c> wins outright;
///     otherwise the platform default — <c>%LOCALAPPDATA%\Coursier\jvm</c> on
///     Windows, <c>~/Library/Caches/Coursier/jvm</c> on macOS, and
///     <c>$XDG_CACHE_HOME/coursier/jvm</c> (defaulting to <c>~/.cache</c>)
///     elsewhere — with a <c>COURSIER_CACHE</c>-derived candidate appended when
///     that variable is set: it steers the artifact cache only, but settings
///     that move it are easily confused for the JVM directory, and an absent
///     path costs one probe.
/// </summary>
public sealed class CoursierJvmProvider(string? jvmDirectory) : IJvmProvider
{
    /// <inheritdoc />
    public string Name => "coursier";

    /// <summary>Scans the default coursier JVM cache directories.</summary>
    public CoursierJvmProvider() : this(null) { }

    IEnumerable<string> IJvmProvider.GetJavaHomes()
    {
        foreach (var directory in jvmDirectory is null ? ResolveJvmDirectories() : [jvmDirectory])
            foreach (var home in JvmScanning.EnumerateGuarded(directory).SelectMany(JvmScanning.ProbeNested))
                yield return home;
    }

    private static IEnumerable<string> ResolveJvmDirectories()
    {
        var jvmCache = Environment.GetEnvironmentVariable("COURSIER_JVM_CACHE");
        if (!string.IsNullOrWhiteSpace(jvmCache))
        {
            yield return jvmCache;
            yield break;
        }

        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
                yield return Path.Combine(localAppData, "Coursier", "jvm");
        }
        else if (OperatingSystem.IsMacOS())
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(profile))
                yield return Path.Combine(profile, "Library", "Caches", "Coursier", "jvm");
        }
        else
        {
            var cacheHome = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
            if (string.IsNullOrWhiteSpace(cacheHome))
            {
                var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                cacheHome = string.IsNullOrEmpty(profile) ? null : Path.Combine(profile, ".cache");
            }

            if (!string.IsNullOrWhiteSpace(cacheHome))
                yield return Path.Combine(cacheHome, "coursier", "jvm");
        }

        var cache = Environment.GetEnvironmentVariable("COURSIER_CACHE");
        if (!string.IsNullOrWhiteSpace(cache))
            yield return Path.Combine(cache, "jvm");
    }
}
