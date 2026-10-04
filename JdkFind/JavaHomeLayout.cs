namespace JdkFind;

/// <summary>
///     Recognizes Java home directories across the layouts found in the wild:
///     a plain home (<c>bin/java</c> + <c>release</c>, with the JDK 8 inner JRE
///     variant <c>jre/bin/java</c> accepted), the macOS bundle layout
///     (<c>&lt;name&gt;.jdk/Contents/Home</c>) and the Homebrew keg layout
///     (<c>libexec/openjdk.jdk/Contents/Home</c>). This is the shared validation
///     contract behind <see cref="IJvmProvider" /> — providers return paths that
///     pass <see cref="Probe" />, and custom implementations should validate the
///     same way.
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
    internal static string JavaExecutablePath(string homeDirectory)
    {
        var executable = Path.Combine(homeDirectory, "bin", JavaExecutableName);
        return File.Exists(executable)
            ? executable
            : Path.Combine(homeDirectory, "jre", "bin", JavaExecutableName);
    }

    private static bool IsJavaHome(string homeDirectory) =>
        File.Exists(Path.Combine(homeDirectory, "release")) &&
        (File.Exists(Path.Combine(homeDirectory, "bin", JavaExecutableName)) ||
         File.Exists(JreJavaExecutablePath(homeDirectory)));

    internal static string JreJavaExecutablePath(string homeDirectory) =>
        Path.Combine(homeDirectory, "jre", "bin", JavaExecutableName);

    private static string[] CandidateLayouts(string candidateDirectory) =>
    [
        candidateDirectory,
        Path.Combine(candidateDirectory, "Contents", "Home"),
        Path.Combine(candidateDirectory, "libexec", "openjdk.jdk", "Contents", "Home"),
    ];
}
