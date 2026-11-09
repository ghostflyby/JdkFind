namespace JdkFind.Providers;

/// <summary>
///     Scans the JVMs coursier installs under its cache's <c>jvm</c> directory
///     (the <c>jvm/&lt;name&gt;/&lt;version&gt;</c> shape, e.g.
///     <c>jvm/temurin-21/21.0.5+11</c>). <c>COURSIER_JVM_CACHE</c> wins outright;
///     otherwise the platform default — <c>%LOCALAPPDATA%\Coursier\jvm</c> on
///     Windows, <c>~/Library/Caches/Coursier/jvm</c> on macOS, and
///     <c>$XDG_CACHE_HOME/coursier/jvm</c> (defaulting to <c>~/.cache</c>)
///     elsewhere — with a <c>COURSIER_CACHE</c>-derived prefix appended when that
///     variable is set: it steers the artifact cache only, but settings that move
///     it are easily confused for the JVM directory, and an absent path costs one
///     enumeration. The prefixes deliberately overlap — each cache root plus its
///     name directories — so the flat <c>jvm/&lt;home&gt;</c> shape is probed
///     under the root while the nested shape is probed under the name.
/// </summary>
public sealed class CoursierJvmProvider(string? jvmDirectory) : ICommonPrefixesJvmProvider
{
    /// <inheritdoc />
    public string Name => "coursier";

    /// <summary>Scans the default coursier JVM cache directories.</summary>
    public CoursierJvmProvider() : this(ResolveDefaultJvmDirectory()) { }

    IEnumerable<string> ICommonPrefixesJvmProvider.GetCommonPrefixes()
    {
        foreach (var directory in ResolvePrefixes())
        {
            yield return directory;
            foreach (var name in JvmScanning.EnumerateGuarded(directory))
                yield return name;
        }
    }

    private IEnumerable<string> ResolvePrefixes()
    {
        // An injected directory is the whole story — no environment fallbacks.
        if (jvmDirectory is not null)
        {
            yield return jvmDirectory;
            yield break;
        }

        var jvmCache = Environment.GetEnvironmentVariable("COURSIER_JVM_CACHE");
        if (!string.IsNullOrWhiteSpace(jvmCache))
        {
            yield return jvmCache;
            yield break;
        }

        var defaultDirectory = ResolveDefaultJvmDirectory();
        if (defaultDirectory is not null)
            yield return defaultDirectory;

        var cache = Environment.GetEnvironmentVariable("COURSIER_CACHE");
        if (!string.IsNullOrWhiteSpace(cache))
            yield return Path.Combine(cache, "jvm");
    }

    private static string? ResolveDefaultJvmDirectory()
    {
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
