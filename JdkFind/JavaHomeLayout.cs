namespace JdkFind;

/// <summary>
///     Recognizes Java home directories across the layouts found in the wild:
///     a plain home (<c>bin/java</c> + <c>release</c>, with the JDK 8 inner JRE
///     variant <c>jre/bin/java</c> accepted), the macOS bundle layout
///     (<c>&lt;name&gt;.jdk/Contents/Home</c>) and the Homebrew keg layouts — the
///     macOS <c>libexec/&lt;name&gt;.jdk/Contents/Home</c> bundle that the JDK formulae
///     install (<c>openjdk.jdk</c>, <c>graalvm.jdk</c>) and the Linux image placed
///     straight into <c>libexec</c>. This is the shared validation contract behind
///     <see cref="IJvmProvider" /> — providers return paths that pass
///     <see cref="Probe" />, and custom implementations should validate the same
///     way.
/// </summary>
public static class JavaHomeLayout
{
    /// <summary>
    ///     Probes a candidate directory for a Java home under any known layout and
    ///     returns the home directory itself — which may be a subdirectory of the
    ///     candidate for bundle and keg layouts — or null when no layout matches.
    /// </summary>
    public static string? Probe(string candidateDirectory)
    {
        if (string.IsNullOrWhiteSpace(candidateDirectory))
            return null;

        foreach (var layout in CandidateLayouts(candidateDirectory))
            if (IsJavaHome(layout))
                return layout;

        return null;
    }

    internal static string JavaExecutableName => OperatingSystem.IsWindows() ? "java.exe" : "java";

    internal static string CompilerExecutableName => OperatingSystem.IsWindows() ? "javac.exe" : "javac";

    /// <summary>Resolves the java executable of a validated home: <c>bin/java</c>,
    /// falling back to the JDK 8 inner JRE layout (<c>jre/bin/java</c>).</summary>
    internal static string JavaExecutablePath(string homeDirectory) =>
        FindExecutable(homeDirectory, JavaExecutableName) ?? JreJavaExecutablePath(homeDirectory);

    private static string JreJavaExecutablePath(string homeDirectory) =>
        Path.Combine(homeDirectory, "jre", "bin", JavaExecutableName);

    /// <summary>Searches a validated home for the named executable: <c>bin</c>
    /// first, falling back to the JDK 8 inner JRE layout (<c>jre/bin</c>). On
    /// Windows a missing <c>.exe</c> suffix is tried as well. Null when no
    /// candidate exists.</summary>
    internal static string? FindExecutable(string homeDirectory, string name)
    {
        foreach (var directory in new[] { "bin", Path.Combine("jre", "bin") })
            foreach (var candidate in ExecutableNameCandidates(name))
            {
                var executable = Path.Combine(homeDirectory, directory, candidate);
                if (File.Exists(executable))
                    return executable;
            }

        return null;
    }

    private static IEnumerable<string> ExecutableNameCandidates(string name)
    {
        yield return name;
        if (OperatingSystem.IsWindows() && !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            yield return name + ".exe";
    }

    private static bool IsJavaHome(string homeDirectory) =>
        File.Exists(Path.Combine(homeDirectory, "release")) &&
        (File.Exists(Path.Combine(homeDirectory, "bin", JavaExecutableName)) ||
         File.Exists(JreJavaExecutablePath(homeDirectory)));

    /// <summary>The bundle directory names the known JDK formulae install inside a
    /// Homebrew keg's <c>libexec</c>.</summary>
    private static readonly string[] KegBundleNames = ["openjdk.jdk", "graalvm.jdk"];

    /// <summary>
    ///     The layouts a candidate directory may hold, in probe order. Everything is
    ///     a fixed path — the bundle names come from the list above rather than a
    ///     directory listing — because <see cref="Probe" /> runs for every candidate
    ///     a provider reports, so it must not enumerate the file system to decide.
    /// </summary>
    private static IEnumerable<string> CandidateLayouts(string candidateDirectory)
    {
        yield return candidateDirectory;
        yield return Path.Combine(candidateDirectory, "Contents", "Home");

        // Homebrew keg: the image sits in libexec, directly (Linux formulae) or
        // inside the formula's .jdk bundle (macOS).
        yield return Path.Combine(candidateDirectory, "libexec");
        foreach (var bundle in KegBundleNames)
            yield return Path.Combine(candidateDirectory, "libexec", bundle, "Contents", "Home");
    }
}
