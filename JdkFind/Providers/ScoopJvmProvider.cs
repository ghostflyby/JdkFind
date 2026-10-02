namespace JdkFind.Providers;

/// <summary>
///     Scans Scoop installs at <c>&lt;scoop&gt;/apps/&lt;name&gt;/current</c>, honoring the
///     <c>SCOOP</c> and <c>SCOOP_GLOBAL</c> environment variables with the default
///     <c>~\scoop</c>. Scoop manages the fixed <c>current</c> leaf; the sibling version
///     directories are internal storage, so only <c>current</c> validates.
/// </summary>
public sealed class ScoopJvmProvider(IEnumerable<string> prefixes) : ICommonPrefixesJvmProvider
{
    public string Name => "scoop";

    private readonly string[] prefixList = [.. prefixes.Where(p => !string.IsNullOrWhiteSpace(p))];

    public ScoopJvmProvider() : this(ResolveDefaultPrefixes()) { }

    IEnumerable<string> ICommonPrefixesJvmProvider.GetCommonPrefixes() =>
        prefixList.SelectMany(prefix => JvmScanning.EnumerateGuarded(Path.Combine(prefix, "apps")));

    string? ICommonPrefixesJvmProvider.GetJavaHome(string subDirectory) =>
        string.Equals(Path.GetFileName(subDirectory), "current", StringComparison.OrdinalIgnoreCase)
            ? ICommonPrefixesJvmProvider.GetJavaHomeDefault(subDirectory)
            : null;

    private static IEnumerable<string> ResolveDefaultPrefixes()
    {
        if (!OperatingSystem.IsWindows())
            yield break;

        var scoop = Environment.GetEnvironmentVariable("SCOOP");
        if (!string.IsNullOrWhiteSpace(scoop))
        {
            yield return scoop;
        }
        else
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(profile))
                yield return Path.Combine(profile, "scoop");
        }

        var global = Environment.GetEnvironmentVariable("SCOOP_GLOBAL");
        if (!string.IsNullOrWhiteSpace(global))
            yield return global;
    }
}
