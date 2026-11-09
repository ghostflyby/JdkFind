using System.Security;

namespace JdkFind.Providers;

/// <summary>
///     Scans conda environments for a bundled JDK (conda-forge's <c>openjdk</c>
///     installs into the environment prefix). Two sources: the active
///     environment (<c>CONDA_PREFIX</c>) and conda's own environment registry
///     <c>~/.conda/environments.txt</c>. Environments without a JDK fail the
///     layout probe and contribute nothing.
/// </summary>
public sealed class CondaJvmProvider(string? environmentsList, string? condaPrefix) : IJvmProvider
{
    /// <inheritdoc />
    public string Name => "conda";

    /// <summary>Scans the active environment and the default environment registry.</summary>
    public CondaJvmProvider() : this(null, null) { }

    IEnumerable<string> IJvmProvider.GetJavaHomes()
    {
        var prefix = condaPrefix ?? Environment.GetEnvironmentVariable("CONDA_PREFIX");
        if (!string.IsNullOrWhiteSpace(prefix) && JavaHomeLayout.Probe(prefix) is { } home)
            yield return home;

        foreach (var environment in ReadEnvironments(environmentsList))
            if (JavaHomeLayout.Probe(environment) is { } environmentHome)
                yield return environmentHome;
    }

    private static IEnumerable<string> ReadEnvironments(string? environmentsList)
    {
        var list = environmentsList;
        if (string.IsNullOrWhiteSpace(list))
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(profile))
                yield break;

            list = Path.Combine(profile, ".conda", "environments.txt");
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(list);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            // A missing or unreadable registry is not a scan failure.
            yield break;
        }

        foreach (var line in lines)
        {
            var environment = line.Trim();
            if (environment.Length > 0)
                yield return environment;
        }
    }
}
