namespace JdkFind;

/// <summary>
///     Recognizes Java home directories across the layouts found in the wild:
///     a plain home (<c>bin/java</c> + <c>release</c>), the macOS bundle layout
///     (<c>&lt;name&gt;.jdk/Contents/Home</c>) and the Homebrew keg layout
///     (<c>libexec/openjdk.jdk/Contents/Home</c>).
/// </summary>
internal static class JavaHomeLayout
{
    internal static string? Probe(string candidateDirectory)
    {
        if (string.IsNullOrWhiteSpace(candidateDirectory))
            return null;

        foreach (var layout in CandidateLayouts(candidateDirectory))
            if (IsJavaHome(layout))
                return layout;

        return null;
    }

    internal static string JavaExecutableName => OperatingSystem.IsWindows() ? "java.exe" : "java";

    private static bool IsJavaHome(string homeDirectory) =>
        File.Exists(Path.Combine(homeDirectory, "bin", JavaExecutableName)) &&
        File.Exists(Path.Combine(homeDirectory, "release"));

    private static string[] CandidateLayouts(string candidateDirectory) =>
    [
        candidateDirectory,
        Path.Combine(candidateDirectory, "Contents", "Home"),
        Path.Combine(candidateDirectory, "libexec", "openjdk.jdk", "Contents", "Home"),
    ];
}
