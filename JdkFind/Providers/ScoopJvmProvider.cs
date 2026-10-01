namespace JdkFind.Providers;

/// <summary>
///     Scans Scoop installs at <c>&lt;scoop&gt;/apps/&lt;name&gt;/current</c>, honoring the
///     <c>SCOOP</c> and <c>SCOOP_GLOBAL</c> environment variables with the default
///     <c>~\scoop</c>.
/// </summary>
public sealed class ScoopJvmProvider(IEnumerable<string> prefixes) : JvmProviderBase
{
    public override string Name => "scoop";

    private readonly string[] prefixes = [.. prefixes.Where(p => !string.IsNullOrWhiteSpace(p))];

    public ScoopJvmProvider() : this(ResolveDefaultPrefixes()) { }

    public override IAsyncEnumerable<string> GetJavaHomesAsync(CancellationToken cancellationToken = default) =>
        ScanNested(prefixes.Select(prefix => Path.Combine(prefix, "apps")), "current").ToAsyncEnumerable();

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
