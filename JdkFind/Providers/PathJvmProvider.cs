namespace JdkFind.Providers;

/// <summary>
///     Scans the <c>PATH</c> environment variable. Entries are probed as Java homes
///     directly, and entries of the form <c>&lt;home&gt;/bin</c> are probed one level up.
/// </summary>
public sealed class PathJvmProvider(string? path) : IJvmProvider
{
    /// <inheritdoc />
    public string Name => "path";

    /// <summary>Scans <c>PATH</c>.</summary>
    public PathJvmProvider() : this(Environment.GetEnvironmentVariable("PATH"))
    {
    }

    /// <inheritdoc />
    public IEnumerable<string> GetJavaHomes()
    {
        if (string.IsNullOrWhiteSpace(path))
            return [];

        return Enumerate(path.Split(Path.PathSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static IEnumerable<string> Enumerate(IEnumerable<string> entries) =>
        entries.Select(HomeFromEntry).OfType<string>();

    private static string? HomeFromEntry(string entry)
    {
        if (JavaHomeLayout.Probe(entry) is { } home)
            return home;

        // PATH entries are often <home>/bin; probe one level up.
        var trimmed = entry.TrimEnd('/', '\\');
        return Path.GetFileName(trimmed).Equals("bin", StringComparison.OrdinalIgnoreCase) &&
               Path.GetDirectoryName(trimmed) is { } parent
            ? JavaHomeLayout.Probe(parent)
            : null;
    }
}
