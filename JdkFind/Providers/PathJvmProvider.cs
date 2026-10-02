namespace JdkFind.Providers;

/// <summary>
///     Scans the <c>PATH</c> environment variable. Entries are probed as Java homes
///     directly, and entries of the form <c>&lt;home&gt;/bin</c> are probed one level up.
/// </summary>
public sealed class PathJvmProvider(string? path) : JvmProviderBase
{
    public override string Name => "path";

    public PathJvmProvider() : this(Environment.GetEnvironmentVariable("PATH"))
    {
    }

    public override IEnumerable<string> GetJavaHomes()
    {
        if (string.IsNullOrWhiteSpace(path))
            yield break;

        foreach (var home in Enumerate(path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            yield return home;
    }

    private static IEnumerable<string> Enumerate(IEnumerable<string> entries)
    {
        foreach (var entry in entries)
        {
            if (Probe(entry) is { } home)
            {
                yield return home;
                continue;
            }

            // PATH entries are often <home>/bin; probe one level up.
            var trimmed = entry.TrimEnd('/', '\\');
            if (Path.GetFileName(trimmed).Equals("bin", StringComparison.OrdinalIgnoreCase) &&
                Path.GetDirectoryName(trimmed) is { } parent &&
                Probe(parent) is { } parentHome)
            {
                yield return parentHome;
            }
        }
    }
}
