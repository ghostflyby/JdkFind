using System.Text;

namespace JdkFind.Cli;

/// <summary>
///     The shell-completion scripts carried as manifest resources. The project
///     file embeds each script from completions/ under a
///     <c>jdkfind.completions.&lt;shell&gt;</c> logical name; a packaging build can
///     drop shells via the JdkFindCompletionsExclude MSBuild property, so
///     which scripts actually exist is probed at runtime, not assumed.
/// </summary>
internal static class Completions
{
    private static readonly (string Shell, string Resource)[] Registry =
    [
        ("bash", "jdkfind.completions.bash"),
        ("zsh", "jdkfind.completions.zsh"),
        ("fish", "jdkfind.completions.fish"),
        ("powershell", "jdkfind.completions.powershell"),
    ];

    private static readonly HashSet<string> Embedded =
        [.. typeof(Completions).Assembly.GetManifestResourceNames()];

    /// <summary>The shells whose script this build carries, in registry order.</summary>
    internal static IReadOnlyList<string> AvailableShells() =>
        Registry.Where(entry => Embedded.Contains(entry.Resource))
            .Select(entry => entry.Shell)
            .ToList();

    /// <summary>True for the canonical shell names the command accepts.</summary>
    internal static bool IsKnownShell(string shell) =>
        Registry.Any(entry => entry.Shell.Equals(shell, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns the script for a known shell, or null when this build
    /// excluded it.</summary>
    internal static string? ReadScript(string shell)
    {
        var resource = Registry.Where(entry => entry.Shell.Equals(shell, StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.Resource)
            .SingleOrDefault();

        if (resource is null || !Embedded.Contains(resource))
            return null;

        using var stream = typeof(Completions).Assembly.GetManifestResourceStream(resource);
        if (stream is null)
            return null;

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
